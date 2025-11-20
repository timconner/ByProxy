using Microsoft.VisualBasic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ByProxy.Services {
    public class ConfigurationService : IHostedService {
        private readonly ILogger<ConfigurationService> _logger;
        private readonly IConfiguration _config;
        private readonly IDbContextFactory<ProxyDb> _dbFactory;
        private readonly ProxyStateService _proxyState;

        private SemaphoreSlim _configSemaphore = new SemaphoreSlim(1, 1);

        public bool ConfigConfirmed { get; private set; }

        public DateTime? ConfigConfirmExpiration { get; private set; }
        public event EventHandler<DateTime?>? AwaitingConfirm;

        private CancellationTokenSource? _configConfirmedOrCanceled;


        public TimeSpan AdminSessionLimit { get; private set; } = TimeSpan.FromHours(8);
        public bool AdminInitialized { get; private set; } = false;

        public bool ChangesPending { get; private set; }
        public event EventHandler<bool>? PendingChanged;

        public ConfigurationService(
            ILogger<ConfigurationService> logger,
            IConfiguration config,
            IDbContextFactory<ProxyDb> dbFactory,
            ProxyStateService proxyState
        ) {
            _logger = logger;
            _config = config;
            _dbFactory = dbFactory;
            _proxyState = proxyState;
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            _logger.LogInformation($"Current Running Configuration: {_proxyState.RunningConfig.Revision}");

            int? adminSessionMinutes = _config.GetValue<int?>("Proxy:AdminSessionMinutes", null);
            if (adminSessionMinutes.HasValue) {
                AdminSessionLimit = TimeSpan.FromMinutes(Math.Min(adminSessionMinutes.Value, 5));
            }

            using (var db = _dbFactory.CreateDbContext()) {
                var roles = await db.AuthRoles.Select(r => r.Name).ToListAsync();
                var missingRoles = AuthRoles.AssignableRoles.Except(roles).ToList();
                if (missingRoles.Count > 0) {
                    _logger.LogInformation($"Seeding database with auth roles: {string.Join(", ", missingRoles)}");
                    foreach (string role in missingRoles) {
                        db.AuthRoles.Add(new AuthRole { Name = role });
                    }
                    await db.SaveChangesAsync();
                }

                var currentConfig = await db.Configurations
                    .AsNoTracking()
                    .SelectSpecificRevision(_proxyState.RunningConfig.Revision);
                if (currentConfig == null) throw new Exception("Running config not found in database.");

                var candidateConfig = await db.Configurations.AsNoTracking().SelectCandidateConfig();
                if (candidateConfig == null) {
                    var nextRevision = await db.Configurations.GetNextAvailableRevision();
                    db.Configurations.Add(currentConfig.Clone(nextRevision));
                    _proxyState.UpdateCandidateRevision(nextRevision);
                    await db.SaveChangesAsync();
                } else {
                    _proxyState.UpdateCandidateRevision(candidateConfig.Revision);
                }
                await Task.WhenAll(CheckAndRevertErrantCandidates(db), UpdateHasChangesPending());

                ConfigConfirmed = currentConfig.Confirmed;
                if (!currentConfig.Confirmed) {
                    _logger.LogWarning($"Startup configuration has not been confirmed. Starting {currentConfig.ConfirmSeconds} second confirm window.");
                    _configConfirmedOrCanceled = new();
                    _ = AwaitConfirm(currentConfig.ConfirmSeconds, _configConfirmedOrCanceled.Token);
                }
            }

            await CheckAdminInitialized();
        }

        public async Task<bool> CheckAdminInitialized() {
            using (var db = _dbFactory.CreateDbContext()) {
                var adminCount = await db.Users
                    .Where(user => user.Roles.Any(role => role.Name == AuthRoles.Admin))
                    .CountAsync();
                AdminInitialized = adminCount > 0;
            }
            return AdminInitialized;
        }

        private async Task CheckAndRevertErrantCandidates(ProxyDb db) {
            var errantCandidates = await db.Configurations
                    .Where(_ => _.Committed == false && _.Reverted == false && _.Revision != _proxyState.CandidateConfigRevision)
                    .ToListAsync();
            if (errantCandidates.Count > 0) {
                foreach (var errantCandidate in errantCandidates) {
                    _logger.LogWarning($"Errant candidate '{errantCandidate.Revision}' found. Setting as reverted.");
                    errantCandidate.Reverted = true;
                }
                await db.SaveChangesAsync();
            }
        }

        private async Task AwaitConfirm(int confirmSeconds, CancellationToken cancellationToken) {
            ConfigConfirmExpiration = DateTime.UtcNow.AddSeconds(confirmSeconds);
            AwaitingConfirm?.Invoke(this, ConfigConfirmExpiration);
            try {
                await Task.Delay(TimeSpan.FromSeconds(confirmSeconds), cancellationToken);
            } catch (OperationCanceledException) { }
            ConfigConfirmExpiration = null;
            AwaitingConfirm?.Invoke(this, ConfigConfirmExpiration);
            await _configSemaphore.WaitAsync();
            try {
                if (!ConfigConfirmed) {
                    _logger.LogWarning("Failed to confirm changes. Reverting to last known good configuration.");
                    await RevertUnconfirmedConfiguration();
                } else {
                    _logger.LogWarning("Changes confirmed.");
                }
            } finally {
                _configSemaphore.Release();
            }
        }

        private async Task DiscardCandidateConfig(int newCandidateBasedOnRevision) {
            using var db = _dbFactory.CreateDbContext();
            var startingCandidateRevision = _proxyState.CandidateConfigRevision;
            await using var transaction = await db.Database.BeginTransactionAsync();
            try {
                var currentCandidate = await db.Configurations.SelectSpecificRevision(startingCandidateRevision);
                if (currentCandidate == null) throw new Exception("No candidate configuration found.");
                db.Configurations.Remove(currentCandidate);
                await db.SaveChangesAsync();

                var basisConfig = await db.Configurations.SelectSpecificRevision(newCandidateBasedOnRevision);
                if (basisConfig == null) throw new Exception("Unable to retrieve current configuration from database.");

                var nextRevision = await db.Configurations.GetNextAvailableRevision();
                _proxyState.UpdateCandidateRevision(nextRevision);
                db.Configurations.Add(basisConfig.Clone(nextRevision));
                await db.SaveChangesAsync();

                await CheckAndRevertErrantCandidates(db);

                await transaction.CommitAsync();
            } catch {
                _proxyState.UpdateCandidateRevision(startingCandidateRevision);
                throw;
            }
            await UpdateHasChangesPending();
        }

        private async Task RevertUnconfirmedConfiguration() {
            if (_proxyState.RunningConfig.Revision == 1) throw new Exception("Cannot revert initial configuration.");
            using var db = _dbFactory.CreateDbContext();
            await db.Configurations
                .Where(_ => _.Revision == _proxyState.RunningConfig.Revision)
                .ExecuteUpdateAsync(setters => setters.SetProperty(config => config.Reverted, true));

            var priorConfig = await db.Configurations
                .AsNoTracking()
                .SelectLatestRunningConfig();
            if (priorConfig == null) throw new Exception("Unable to find a valid configuration to revert to.");

            _proxyState.UpdateRunningConfig(priorConfig.AsRunningConfig());
            ConfigConfirmed = true;

            await DiscardCandidateConfig(priorConfig.Revision);
        }

        // Admin User Functions
        public async Task RequestCommit(int confirmSeconds) {
            if (!ConfigConfirmed) throw new Exception("Currently waiting for latest configuration to be confirmed.");
            if (_configSemaphore.CurrentCount == 0) throw new Exception("Config changes currently in progress.");
            await _configSemaphore.WaitAsync();
            try {
                using var db = _dbFactory.CreateDbContext();

                var candidateConfig = await db.Configurations
                    .Where(_ => _.Revision == _proxyState.CandidateConfigRevision)
                    .FirstOrDefaultAsync();
                if (candidateConfig == null) throw new Exception("No candidate configuration found.");

                candidateConfig.Committed = true;
                candidateConfig.Confirmed = false;
                candidateConfig.ConfirmSeconds = confirmSeconds;
                candidateConfig.CommittedAt = DateTime.UtcNow;

                var nextRevision = await db.Configurations.GetNextAvailableRevision();
                var newCandidate = candidateConfig.Clone(nextRevision);
                
                await using var transaction = await db.Database.BeginTransactionAsync();
                try {
                    db.Configurations.Add(newCandidate);
                    _proxyState.UpdateCandidateRevision(nextRevision);
                    await db.SaveChangesAsync();
                } catch {
                    _proxyState.UpdateCandidateRevision(candidateConfig.Revision);
                    throw;
                }

                await CheckAndRevertErrantCandidates(db);

                _logger.LogInformation("Candidate config saved to database. Applying update...");
                _proxyState.UpdateRunningConfig(candidateConfig.AsRunningConfig());
                ChangesPending = false;
                PendingChanged?.Invoke(this, false);
                ConfigConfirmed = false;
                await transaction.CommitAsync();

                if (_proxyState.KestrelRestarting.IsCancellationRequested) {
                    _logger.LogWarning($"Changes applied. Application is restarting.");
                } else {
                    _logger.LogWarning($"Changes applied. Starting {confirmSeconds} second confirm window.");
                    if (_configConfirmedOrCanceled != null) _configConfirmedOrCanceled.Dispose();
                    _configConfirmedOrCanceled = new();
                    _ = AwaitConfirm(confirmSeconds, _configConfirmedOrCanceled.Token);
                }
            } finally {
                _configSemaphore.Release();
            }
        }

        public async Task ConfirmCommit() {
            await _configSemaphore.WaitAsync();
            try {
                _configConfirmedOrCanceled?.Cancel();
                using (var db = _dbFactory.CreateDbContext()) {
                    await db.Configurations
                        .Where(_ => _.Revision == _proxyState.RunningConfig.Revision)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(config => config.Confirmed, true));
                }
                ConfigConfirmed = true;
            } finally {
                _configSemaphore.Release();
            }
        }

        public async Task RequestCandidateConfigDiscard() {
            await _configSemaphore.WaitAsync();
            try {
                await DiscardCandidateConfig(_proxyState.RunningConfig.Revision);
            } catch (Exception ex) {
                throw new Exception($"Failed to discard changes: {ex.Message}", ex);
            } finally {
                _configSemaphore.Release();
            }
        }
        public async Task RequestCancelConfirmAndRollback() {
            await _configSemaphore.WaitAsync();
            try {
                if (ConfigConfirmed) throw new Exception("Current configuration has already been confirmed and cannot be canceled.");
                _configConfirmedOrCanceled?.Cancel();
            } finally {
                _configSemaphore.Release();
            }
        }

        public async Task PromoteRevisionToCandidate(int revisionToPromote) {
            await DiscardCandidateConfig(revisionToPromote);
        }

        public async Task UpdateHasChangesPending() {
            using var db = _dbFactory.CreateDbContext();

            var candidateConfig = await db.Configurations
                .AsNoTracking()
                .SelectSpecificRevision(_proxyState.CandidateConfigRevision);
            if (candidateConfig == null) throw new Exception("Candidate config not found in database.");

            var changesPending = candidateConfig.GenerateConfigHash() != _proxyState.RunningConfig.ConfigHash;
            if (changesPending != ChangesPending) {
                ChangesPending = changesPending;
                PendingChanged?.Invoke(this, ChangesPending);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            await _configSemaphore.WaitAsync(cancellationToken);
            _configSemaphore.Release();
        }
    }
}
