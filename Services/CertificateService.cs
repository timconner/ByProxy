using System.Collections.Concurrent;
using System.Data.Common;

namespace ByProxy.Services {
    public class CertificateService : IHostedService {
        private readonly ILogger<CertificateService> _logger;
        private readonly IDbContextFactory<ProxyDb> _dbFactory;
        private readonly ProxyStateService _proxyState;
        private readonly AcmeClientService _acmeClient;

        private Task? _executeTask;
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private CancellationTokenSource _cacheUpdated = new CancellationTokenSource();

        private X509Certificate2 _adminCert = default!;
        private X509Certificate2? _fallbackCert = default!;

        private readonly ConcurrentDictionary<Guid, X509Certificate2> _certFileCache = new();

        private readonly SemaphoreSlim _signal = new(0, 1);

        // Guid = Cert Id
        private List<AcmeCache> _acmeCache = new();

        public X509Certificate2 AdminCertificateSelector(ConnectionContext? context, string? hostname) {
            return _adminCert;
        }

        public X509Certificate2? CertificateSelector(ConnectionContext? context, string? hostname) {
            if (string.IsNullOrEmpty(hostname)) return _fallbackCert;
            if (!_proxyState.RunningConfig.SniMaps.TryGetCertIdByHost(hostname, out var certId)) return _fallbackCert;
            return GetCertificate(certId);
        }


        public X509Certificate2 GetCertificate(Guid certId, bool forceReload = false) {
            if (forceReload) {
                return _certFileCache.AddOrUpdate(
                    key: certId,
                    addValue: Certificates.ImportCertFromDisk(certId),
                    updateValueFactory: (id, oldCert) => Certificates.ImportCertFromDisk(certId)
                );
            }
            return _certFileCache.GetOrAdd(certId, Certificates.ImportCertFromDisk);
        }

        public CertificateService(
            ILogger<CertificateService> logger,
            IDbContextFactory<ProxyDb> dbFactory,
            ProxyStateService proxyState,
            AcmeClientService acmeClient
        ) {
            _logger = logger;
            _dbFactory = dbFactory;
            _proxyState = proxyState;
            _acmeClient = acmeClient;
        }

        private sealed class AcmeCache {
            public AcmeCert Metadata { get; init; }
            public DateTime RenewAt { get; init; }

            public AcmeCache(AcmeCert metadata, X509Certificate2 cert) {
                Metadata = metadata;
                var validityPeriodDays = cert.NotAfter.Subtract(cert.NotBefore).TotalDays;
                // if short-lived, renew at half-life, else when 1/3 validity period remains
                double renewalThresholdDays = validityPeriodDays <= 30 ? validityPeriodDays * 0.5 : validityPeriodDays * (2.0 / 3.0);
                RenewAt = cert.NotBefore.AddDays(renewalThresholdDays);
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Starting Certificate Service...");

            using var db = _dbFactory.CreateDbContext();

            _proxyState.OnConfigChange += RunningConfigUpdateHandler;

            _logger.LogInformation("Building certificate cache...");
            await RebuildCertCache();

            _executeTask = ExecuteAsync(_stoppingCts.Token);

            _logger.LogInformation("Certificate Service Started");
        }

        private async void RunningConfigUpdateHandler(object? sender, EventArgs e) {
            await RebuildCertCache();
        }

        private async Task RebuildCertCache() {
            using var db = _dbFactory.CreateDbContext();

            var adminCert = await db.ServerCerts.FirstOrDefaultAsync(_ => _.Id == _proxyState.RunningConfig.AdminCert);
            if (adminCert != null) {
                try {
                    _adminCert = GetCertificate(adminCert.Id);
                } catch (Exception ex) {
                    adminCert = null;
                    _logger.LogError($"Failed to import admin certificate: {ex.Message}", ex);
                }
            }
            if (adminCert == null) {
                // Failsafe in case no valid certificate was found.
                _logger.LogWarning("Valid admin certificate not found, generating ephemeral cert...");
                using var newAdminCert = Certificates.GenerateSelfSignedCert(Certificates.LoopbackHosts);
                _adminCert = new X509Certificate2(
                    newAdminCert.Export(X509ContentType.Pfx, (string?)null),
                    (string?)null,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable
                );
            }

            if (_proxyState.RunningConfig.FallbackCert == null) {
                _fallbackCert = null;
            } else {
                try {
                    _fallbackCert = GetCertificate(_proxyState.RunningConfig.FallbackCert.Value);
                } catch (Exception ex) {
                    _fallbackCert = null;
                    _logger.LogError($"Failed to import fallback certificate, connections without SNI maps will fail: {ex.Message}", ex);
                }
            }

            var activeCerts = await db.SniMaps
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(_ => _.ConfigRevision == _proxyState.RunningConfig.Revision)
                .Select(_ => _.Certificate)
                .Distinct()
                .ToListAsync();

            var activeCertIds = new HashSet<Guid>();
            var acmeCache = new List<AcmeCache>();
            foreach (var cert in activeCerts) {
                try {
                    if (cert is ServerCert) {
                        var importedCert = GetCertificate(cert.Id, true);
                        if (cert is AcmeCert acmeCert) {
                            acmeCache.Add(new AcmeCache(acmeCert, importedCert));
                        }
                    }
                } catch (Exception ex) {
                    _logger.LogError($"Failed to cache certificate {cert.Id}: {ex.Message}", ex);
                }
            }
            _acmeCache = acmeCache;

            var certsToRemove = _certFileCache.Keys.Except(activeCertIds);
            foreach (var certId in certsToRemove) {
                if (certId == _proxyState.RunningConfig.AdminCert || certId == _proxyState.RunningConfig.FallbackCert) continue;
                if (_certFileCache.TryRemove(certId, out var staleCert)) {
                    staleCert.Dispose();
                }
            }
        }

        public async Task<List<CertInfo>> CompileAvailableServerCerts(IStringLocalizer strings, bool includeHidden = false) {
            using var db = _dbFactory.CreateDbContext();

            List<ServerCert> certResults;

            if (includeHidden) {
                certResults = await db.ServerCerts
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .ToListAsync();
            } else {
                certResults = await db.ServerCerts
                    .AsNoTracking()
                    .WhereIsActive()
                    .ToListAsync();
            }

            return await CompileAvailableServerCerts(certResults, db, strings);
        }

        public async Task<List<CertInfo>> CompileAvailableServerCerts<T>(IStringLocalizer strings) where T : ServerCert {
            using var db = _dbFactory.CreateDbContext();

            var certResults = await db.ServerCerts
                .AsNoTracking()
                .OfType<T>()
                .WhereIsActive()
                .ToListAsync();

            return await CompileAvailableServerCerts(certResults.Cast<ServerCert>().ToList(), db, strings);
        }

        private async Task<List<CertInfo>> CompileAvailableServerCerts(List<ServerCert> certResults, ProxyDb db, IStringLocalizer strings) {
            var sniMaps = await db.SniMaps
                .AsNoTracking()
                .ToListAsync();

            var fallbackId = await db.Configurations
                .AsNoTracking()
                .Where(_ => _.Revision == _proxyState.CandidateConfigRevision)
                .Select(_ => _.FallbackCertId)
                .FirstAsync();

            var availableCerts = new List<CertInfo>();
            foreach (var result in certResults) {
                var cert = GetCertificate(result.Id);
                var subjectNames = Certificates.GetSubjectAltNames(cert);

                string issuer = cert.Subject == cert.Issuer ? strings["Self-Signed"] : cert.GetNameInfo(X509NameType.SimpleName, forIssuer: true);

                var certMaps = new List<string>();
                foreach (var map in sniMaps.Where(_ => _.CertificateId == result.Id)) {
                    certMaps.Add(map.Host);
                }
                if (result.Id == fallbackId) {
                    certMaps.Add($"*.* ({strings["Fallback"]})");
                }

                var certInfo = new CertInfo(result, cert, subjectNames, issuer, certMaps);
                availableCerts.Add(certInfo);
            }

            return availableCerts;
        }

        public async Task PurgeCertificate(Guid certId) {
            using var db = _dbFactory.CreateDbContext();

            var cert = await db.Certificates.FirstOrDefaultAsync(_ => _.Id == certId);
            if (cert == null) throw new Exception("Certificate not found in database.");

            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Certificates.Remove(cert);
            await db.SaveChangesAsync();

            Certificates.DeleteCertFromDisk(certId);
            _certFileCache.Remove(certId, out _);

            await transaction.CommitAsync();
        }

        public async Task PurgeCertificate(Guid certToPurgeId, Guid replacementCertId) {
            using var db = _dbFactory.CreateDbContext();

            var certToPurge = await db.Certificates.FirstOrDefaultAsync(_ => _.Id == certToPurgeId);
            if (certToPurge == null) throw new Exception("Certificate to purge not found in database.");

            var replacementCert = await db.Certificates.FirstOrDefaultAsync(_ => _.Id == replacementCertId);
            if (replacementCert == null) throw new Exception("Replacement certificate not found in database.");

            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Configurations
                .IgnoreQueryFilters()
                .Where(_ => _.AdminCertId == certToPurge.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(config => config.AdminCertId, replacementCert.Id));

            await db.Configurations
                .IgnoreQueryFilters()
                .Where(_ => _.FallbackCertId == certToPurge.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(config => config.FallbackCertId, replacementCert.Id));

            await db.SniMaps
                .IgnoreQueryFilters()
                .Where(_ => _.CertificateId == certToPurge.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(map => map.CertificateId, replacementCert.Id));

            db.Certificates.Remove(certToPurge);
            await db.SaveChangesAsync();

            Certificates.DeleteCertFromDisk(certToPurge.Id);
            _certFileCache.Remove(certToPurge.Id, out _);

            await transaction.CommitAsync();
        }

        public void TriggerAcmeRenewalTask() {
            try { _signal.Release(); } catch (SemaphoreFullException) { }
        }

        protected async Task ExecuteAsync(CancellationToken stoppingToken) {
            while (!stoppingToken.IsCancellationRequested) {
                bool certRenewed = false;
                var certsToRenew = _acmeCache.Where(_ => _.RenewAt < DateTime.Now && (_.Metadata.LastAttempt == null || DateTime.Now.Subtract(_.Metadata.LastAttempt.Value).TotalHours > 12));
                if (certsToRenew.Any()) {
                    using var db = _dbFactory.CreateDbContext();
                    foreach (var cert in certsToRenew) {
                        try {
                            var dbEntry = await db.AcmeCerts
                                .Include(_ => _.AcmeAccount)
                                .Include(_ => _.Hosts)
                                .FirstOrDefaultAsync(_ => _.Id == cert.Metadata.Id);
                            if (dbEntry == null) continue;

                            cert.Metadata.LastAttempt = dbEntry.LastAttempt = DateTime.UtcNow;
                            await db.SaveChangesAsync();

                            var renewedCert = await _acmeClient.RequestNewCertificateAsync(dbEntry.AcmeAccount, dbEntry.Hosts, null, stoppingToken);
                            Certificates.ExportCertToDisk(dbEntry.Id, renewedCert);
                            certRenewed = true;
                        } catch (Exception ex) {
                            _logger.LogError($"Failed to renew ACME certificate: {ex.Message}", ex);
                        }
                    }
                }
                if (certRenewed) await RebuildCertCache();
                try {
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)) {
                        await Task.WhenAny(
                            Task.Delay(TimeSpan.FromHours(1), cts.Token),
                            _signal.WaitAsync(cts.Token)
                        );
                        cts.Cancel();
                    }
                } catch (OperationCanceledException) { }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            _proxyState.OnConfigChange -= RunningConfigUpdateHandler;
            _signal.Dispose();
            if (_executeTask != null) {
                _stoppingCts.Cancel();
                await _executeTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }
}
