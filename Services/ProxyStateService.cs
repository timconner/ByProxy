namespace ByProxy.Services {
    public interface IProxyStateService { public int CandidateConfigRevision { get; } }

    public class BootstrapProxyStateService : IProxyStateService {
        public int CandidateConfigRevision => 0;
    }

    public class ProxyStateService : IProxyStateService, IDisposable {
        public DateTime StartupTime { get; init; } = DateTime.UtcNow;

        private ILogger<ProxyStateService> _logger;
        private readonly InMemoryConfigProvider _yarpConfig;

        private CancellationTokenSource _restartKestrel;
        public CancellationToken KestrelRestarting => _restartKestrel.Token;

        private CancellationTokenSource _stoppingOrRestarting;
        public CancellationToken StoppingOrRestarting => _stoppingOrRestarting.Token;

        private volatile RunningConfig _runningConfig;
        public RunningConfig RunningConfig => _runningConfig;
        public event EventHandler? OnConfigChange;

        private volatile int _candidateConfigRevision;
        public event EventHandler<int>? CandidateChanged;

        public int CandidateConfigRevision => _candidateConfigRevision;

        public ProxyStateService(
            ILogger<ProxyStateService> logger,
            IHostApplicationLifetime lifetime,
            RunningConfig startupConfig,
            InMemoryConfigProvider yarpConfig
        ) {
            _logger = logger;
            _runningConfig = startupConfig;
            _yarpConfig = yarpConfig;

            _restartKestrel = new();
            _stoppingOrRestarting = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping, _restartKestrel.Token);

            LogErrors();
        }

        public void UpdateRunningConfig(RunningConfig newRunningConfig) {
            if (CheckUpdateRequiresRestart(RunningConfig, newRunningConfig)) {
                _logger.LogWarning("Port changes were detected. Restarting services.");
                _restartKestrel.Cancel();
                return;
            }

            _runningConfig = newRunningConfig;
            _yarpConfig.Update(newRunningConfig.Routes.ToList(), newRunningConfig.Clusters.ToList());
            OnConfigChange?.Invoke(this, EventArgs.Empty);
            
            LogErrors();
        }

        private void LogErrors() {
            foreach (var error in RunningConfig.Errors) {
                _logger.LogError(error);
            }
        }

        public void UpdateCandidateRevision(int newCandidateRevision) {
            _candidateConfigRevision = newCandidateRevision;
            CandidateChanged?.Invoke(this, newCandidateRevision);
        }

        public bool CheckCommitRequiresRestart(RunningConfig newConfig) =>
            CheckUpdateRequiresRestart(_runningConfig, newConfig);

        private bool CheckUpdateRequiresRestart(RunningConfig currentConfig, RunningConfig newConfig) {
            return currentConfig.AdminPort != newConfig.AdminPort
                || currentConfig.AdminListenAny != newConfig.AdminListenAny
                || !currentConfig.HttpPorts.ToHashSet().SetEquals(newConfig.HttpPorts)
                || !currentConfig.HttpsPorts.ToHashSet().SetEquals(newConfig.HttpsPorts)
                || currentConfig.FallbackCert.HasValue != newConfig.FallbackCert.HasValue; // HTTPS Refuse/Fallback behavior changed
        }

        public void RequestKestrelRestart() {
            _restartKestrel.Cancel();
        }

        public void Dispose() {
            _restartKestrel.Dispose();
        }
    }
}
