namespace ByProxy.Services {
    public class CertificateService : IHostedService {
        private readonly ILogger<CertificateService> _logger;
        private readonly IDbContextFactory<ProxyDb> _dbFactory;
        private readonly ProxyStateService _proxyState;
        private readonly AcmeClientService _acmeClient;

        private Task? _executeTask;
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private SemaphoreSlim _cacheRebuildSemaphore = new SemaphoreSlim(1, 1);

        private SslStreamCertificateContext _adminCert = default!;
        private SslStreamCertificateContext? _fallbackCert = default!;

        private readonly ConcurrentDictionary<Guid, SslStreamCertificateContext> _certCache = new();

        private readonly SemaphoreSlim _signal = new(0, 1);

        // Guid = Cert Id
        private List<AcmeCache> _acmeCache = new();

        public SslStreamCertificateContext AdminCertificateSelector(ConnectionContext? context, string? hostname) {
            return _adminCert;
        }

        public SslStreamCertificateContext? CertificateSelector(ConnectionContext? context, string? hostname) {
            if (string.IsNullOrEmpty(hostname)) return _fallbackCert;
            if (!_proxyState.RunningConfig.SniMaps.TryGetCertIdByHost(hostname, out var certId)) return _fallbackCert;
            return GetCertificateContext(certId);
        }

        private SslStreamCertificateContext BuildCertContext(Guid certId) {
            var certificates = Certificates.ImportCertCollectionFromDisk(certId);
            try {
                X509Certificate2? leaf = null;
                var intermediates = new X509Certificate2Collection();
                foreach (var cert in certificates) {
                    if (cert.HasPrivateKey) {
                        if (leaf != null) throw new Exception("Multiple certificates with private keys found in collection.");
                        leaf = cert;
                    } else {
                        intermediates.Add(cert);
                    }
                }

                if (leaf == null) throw new Exception("Leaf certificate not found.");
                return SslStreamCertificateContext.Create(leaf, intermediates);
            } catch {
                certificates.DisposeCollection();
                throw;
            }
        }

        public SslStreamCertificateContext GetCertificateContext(Guid certId, bool forceReload = false) {
            if (forceReload) {
                return _certCache.AddOrUpdate(
                    key: certId,
                    addValueFactory: (id) => BuildCertContext(id),
                    updateValueFactory: (id, oldCert) => BuildCertContext(id)
                );
            }
            return _certCache.GetOrAdd(certId, BuildCertContext);
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
                RenewAt = cert.NotBefore.AddDays(renewalThresholdDays).ToUniversalTime();
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
            try {
                await RebuildCertCache();
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to rebuild certificate cache.");
            }
        }

        private async Task RebuildCertCache() {
            await _cacheRebuildSemaphore.WaitAsync();
            try {
                var runningConfig = _proxyState.RunningConfig;
                using var db = _dbFactory.CreateDbContext();
                var activeCertIds = new HashSet<Guid>();
                var acmeCache = new List<AcmeCache>();

                var adminCert = await db.ServerCerts.FirstOrDefaultAsync(_ => _.Id == runningConfig.AdminCert);
                if (adminCert != null) {
                    try {
                        _adminCert = GetCertificateContext(adminCert.Id, true);
                        activeCertIds.Add(adminCert.Id);
                        if (adminCert is AcmeCert acmeCert) {
                            acmeCache.Add(new AcmeCache(acmeCert, _adminCert.TargetCertificate));
                        }
                    } catch (Exception ex) {
                        adminCert = null;
                        _logger.LogError($"Failed to import admin certificate: {ex.Message}", ex);
                    }
                }
                if (adminCert == null) {
                    // Failsafe in case no valid certificate was found.
                    _logger.LogWarning("Valid admin certificate not found, generating ephemeral cert...");
                    using var newAdminCert = Certificates.GenerateSelfSignedCert(Certificates.LoopbackHosts);
                    _adminCert = SslStreamCertificateContext.Create(
                        X509CertificateLoader.LoadPkcs12(
                            newAdminCert.Export(X509ContentType.Pfx, (string?)null),
                            (string?)null,
                            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable), null, true);
                }

                if (runningConfig.FallbackCert == null) {
                    _fallbackCert = null;
                } else {
                    try {
                        var fallbackCert = await db.ServerCerts.FirstAsync(_ => _.Id == runningConfig.FallbackCert.Value);
                        _fallbackCert = GetCertificateContext(fallbackCert.Id, true);
                        if (activeCertIds.Add(fallbackCert.Id) && fallbackCert is AcmeCert acmeCert) {
                            acmeCache.Add(new AcmeCache(acmeCert, _fallbackCert.TargetCertificate));
                        }
                    } catch (Exception ex) {
                        _fallbackCert = null;
                        _logger.LogError($"Failed to import fallback certificate, connections without SNI maps will fail: {ex.Message}", ex);
                    }
                }

                var activeCerts = await db.SniMaps
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(_ => _.ConfigRevision == runningConfig.Revision)
                    .Select(_ => _.Certificate)
                    .Distinct()
                    .ToListAsync();

                foreach (var cert in activeCerts) {
                    if (activeCertIds.Contains(cert.Id)) continue;
                    try {
                        var importedCert = GetCertificateContext(cert.Id, true);
                        activeCertIds.Add(cert.Id);
                        if (cert is AcmeCert acmeCert) {
                            acmeCache.Add(new AcmeCache(acmeCert, importedCert.TargetCertificate));
                        }
                    } catch (Exception ex) {
                        _logger.LogError($"Failed to cache certificate {cert.Id}: {ex.Message}", ex);
                    }
                }
                _acmeCache = acmeCache;

                var certsToRemove = _certCache.Keys.Except(activeCertIds);
                foreach (var certId in certsToRemove) {
                    if (certId == runningConfig.AdminCert || certId == runningConfig.FallbackCert) continue;
                    if (_certCache.TryRemove(certId, out var staleCert)) {
                        staleCert.TargetCertificate.Dispose();
                        foreach (var cert in staleCert.IntermediateCertificates) {
                            cert.Dispose();
                        }
                    }
                }
            } finally {
                _cacheRebuildSemaphore.Release();
            }
        }

        public async Task<List<CertInfo>> CompileAvailableServerCerts(IStringLocalizer strings, bool includeHidden = false) {
            using var db = _dbFactory.CreateDbContext();

            var certQuery = db.ServerCerts
                .AsNoTracking();

            if (!includeHidden) certQuery = certQuery.WhereIsActive();

            var certResults = await certQuery.ToListAsync();

            return await CompileAvailableServerCerts(certResults, db, strings);
        }

        public async Task<List<CertInfo>> CompileAvailableServerCerts<T>(IStringLocalizer strings, bool includeHidden = false) where T : ServerCert {
            using var db = _dbFactory.CreateDbContext();

            var certQuery = db.ServerCerts
                .AsNoTracking()
                .OfType<T>();

            if (!includeHidden) certQuery = certQuery.WhereIsActive();

            var certResults = await certQuery.ToListAsync();

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
                var cert = GetCertificateContext(result.Id).TargetCertificate;
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

            await transaction.CommitAsync();
            Certificates.DeleteCertFromDisk(certId);
            _certCache.Remove(certId, out _);
        }

        public async Task PurgeCertificate(Guid certToPurgeId, Guid replacementCertId) {
            if (certToPurgeId == replacementCertId) throw new Exception("Replacement certificate cannot be the same as the certificate to purge.");

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

            await transaction.CommitAsync();
            Certificates.DeleteCertFromDisk(certToPurge.Id);
            _certCache.Remove(certToPurge.Id, out _);
        }

        public void TriggerAcmeRenewalTask() {
            try { _signal.Release(); } catch (SemaphoreFullException) { }
        }

        public async Task<bool> RenewCertificate(ProxyDb db, Guid certId, bool rebuildCacheOnSuccess, IProgress<(string Step, string? Detail)>? progress = null, CancellationToken cancellationToken = default) {
            try {
                var dbEntry = await db.AcmeCerts
                    .Include(_ => _.AcmeAccount)
                    .Include(_ => _.Hosts)
                    .FirstOrDefaultAsync(_ => _.Id == certId);
                if (dbEntry == null) throw new Exception($"Certificate {certId} not found in database.");

                dbEntry.LastAttempt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                foreach (var cacheItem in _acmeCache.Where(_ => _.Metadata.Id == certId)) {
                    cacheItem.Metadata.LastAttempt = dbEntry.LastAttempt;
                }

                var renewedCert = await _acmeClient.RequestNewCertificateAsync(dbEntry.AcmeAccount, dbEntry.Hosts, progress, cancellationToken);
                Certificates.ExportCertToDisk(dbEntry.Id, renewedCert);

                if (rebuildCacheOnSuccess) await RebuildCertCache();

                return true;
            } catch (Exception ex) {
                _logger.LogError($"Failed to renew ACME certificate: {ex.Message}", ex);
            }
            return false;
        }

        protected async Task ExecuteAsync(CancellationToken stoppingToken) {
            while (!stoppingToken.IsCancellationRequested) {
                bool certRenewed = false;
                var certsToRenew = _acmeCache.Where(_ => _.RenewAt < DateTime.UtcNow && (_.Metadata.LastAttempt == null || DateTime.UtcNow.Subtract(_.Metadata.LastAttempt.Value).TotalHours > 12));
                if (certsToRenew.Any()) {
                    using var db = _dbFactory.CreateDbContext();
                    foreach (var cert in certsToRenew) {
                        try {
                            var success = await RenewCertificate(db, cert.Metadata.Id, false, null, stoppingToken);
                            if (success) certRenewed = true;
                        } catch (Exception ex) {
                            _logger.LogError($"Failed to renew ACME certificate: {ex.Message}", ex);
                        }
                    }
                }
                if (certRenewed) {
                    try {
                        await RebuildCertCache();
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to rebuild certificate cache after sucessful ACME renewal.");
                    }
                }
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
            if (_executeTask != null) {
                _stoppingCts.Cancel();
                await _executeTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            _signal.Dispose();
        }
    }
}
