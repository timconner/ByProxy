namespace ByProxy.Services {
    using ProxyExportVLatest = ImportExportService.ProxyExportV1_25;
    using SystemSettingsExportVLatest = ImportExportService.SystemSettingsExportV1_25;
    using RouteExportVLatest = ImportExportService.RouteExportV1_25;
    using RequestTransformExportVLatest = ImportExportService.RequestTransformExportV1_25;
    using RequestPathTransformExportVLatest = ImportExportService.RequestPathTransformExportV1_25;
    using RequestMethodTransformExportVLatest = ImportExportService.RequestMethodTransformExportV1_25;
    using RequestHeaderTransformExportVLatest = ImportExportService.RequestHeaderTransformExportV1_25;
    using RequestQueryTransformExportVLatest = ImportExportService.RequestQueryTransformExportV1_25;
    using RouteMatchExportVLatest = ImportExportService.RouteMatchExportV1_25;
    using RouteHeaderMatchExportVLatest = ImportExportService.RouteHeaderMatchExportV1_25;
    using RouteQueryMatchExportVLatest = ImportExportService.RouteQueryMatchExportV1_25;
    using ClusterExportVLatest = ImportExportService.ClusterExportV1_25;
    using DestinationExportVLatest = ImportExportService.DestinationExportV1_25;
    using SniMapExportVLatest = ImportExportService.SniMapExportV1_25;
    using AcmeAccountExportVLatest = ImportExportService.AcmeAccountExportV1_25;
    using AcmeDnsProviderExportVLatest = ImportExportService.AcmeDnsProviderExportV1_25;
    using CertificateAuthorityExportVLatest = ImportExportService.CertificateAuthorityExportV1_25;
    using ServerCertExportVLatest = ImportExportService.ServerCertExportV1_25;
    using IssuedCertExportVLatest = ImportExportService.IssuedCertExportV1_25;
    using AcmeCertExportVLatest = ImportExportService.AcmeCertExportV1_25;
    using ImportedCertExportVLatest = ImportExportService.ImportedCertExportV1_25;
    using AcmeCertHostExportVLatest = ImportExportService.AcmeCertHostExportV1_25;
    using AcmeHttpCertHostExportVLatest = ImportExportService.AcmeHttpCertHostExportV1_25;
    using AcmeDnsCertHostExportVLatest = ImportExportService.AcmeDnsCertHostExportV1_25;
    using EncryptionParametersVLatest = ImportExportService.EncryptionParametersV1_25;
    using EncryptionHeaderVLatest = ImportExportService.EncryptionHeaderV1_25;
    using ResponseTransformExportVLatest = ImportExportService.ResponseTransformExportV1_25;
    using ResponseHeaderTransformExportVLatest = ImportExportService.ResponseHeaderTransformExportV1_25;
    using StaticResponseExportVLatest = ImportExportService.StaticResponseExportV1_25;

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$version")]
    [JsonDerivedType(typeof(ImportExportService.ProxyExportV1_20), "1.20")]
    [JsonDerivedType(typeof(ImportExportService.ProxyExportV1_24), "1.24")]
    [JsonDerivedType(typeof(ImportExportService.ProxyExportV1_25), "1.25")]
    public abstract class ProxyExport {
        public abstract ProxyExport ToNext();
    }

    public partial class ImportExportService {
        private readonly IDbContextFactory<ProxyDb> _dbFactory;
        private readonly ProxyStateService _proxyState;
        private readonly ConfigurationService _config;
        private readonly CertificateService _certService;

        public ImportExportService(
            IDbContextFactory<ProxyDb> dbFactory,
            ProxyStateService proxyState,
            ConfigurationService config,
            CertificateService certService
        ) {
            _dbFactory = dbFactory;
            _proxyState = proxyState;
            _config = config;
            _certService = certService;
        }

        public static async Task<IResult> GenerateDownload(
            [FromServices] ImportExportService service,
            [FromForm] ExportOptions options,
            [FromForm] string? password,
            [FromForm] int? revision = null
        ) {
            ProxyExport export;
            if (revision != null) {
                export = await service.GenerateExportAsync(revision.Value, options);
            } else {
                export = await service.GenerateExportOfCurrentAsync(options);
            }

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = password == null,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            string fileName;
            if (!string.IsNullOrEmpty(password)) {
                fileName = $"byproxy-export-{DateTime.UtcNow:yyyyMMddHHmmss}.json.encrypted";
                return Results.File(service.EncryptExport(jsonBytes, password), "application/octet-stream", fileName);
            } else {
                fileName = $"byproxy-export-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                return Results.File(jsonBytes, "application/octet-stream", fileName);
            }
        }

        private MemoryStream EncryptExport(byte[] contents, string password) {
            byte[] salt = RandomNumberGenerator.GetBytes(EncryptionParametersVLatest.SaltSize);
            byte[] nonce = RandomNumberGenerator.GetBytes(EncryptionParametersVLatest.NonceSize);
            byte[] header = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new EncryptionHeaderVLatest(salt, nonce)));

            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt,
                EncryptionParametersVLatest.Iterations,
                EncryptionParametersVLatest.HashAlgorithm,
                EncryptionParametersVLatest.KeySize);

            byte[] ciphertext = new byte[contents.Length];
            byte[] tag = new byte[EncryptionParametersVLatest.TagSize];

            using (var aes = new AesGcm(key, EncryptionParametersVLatest.TagSize))
                aes.Encrypt(nonce, contents, ciphertext, tag, header);


            var output = new MemoryStream(header.Length + ciphertext.Length + EncryptionParametersVLatest.TagSize);
            output.Write(header);
            output.WriteByte(0x0A); // LF
            output.Write(ciphertext);
            output.Write(tag);
            output.Position = 0;
            return output;
        }

        private Task<ProxyExport> GenerateExportOfCurrentAsync(ExportOptions exportOptions, CancellationToken cancellationToken = default) {
            return GenerateExportAsync(_proxyState.RunningConfig.Revision, exportOptions, cancellationToken);
        }

        private async Task<ProxyExport> GenerateExportAsync(int revisionToExport, ExportOptions exportOptions, CancellationToken cancellationToken = default) {
            using var db = _dbFactory.CreateDbContext();
            var config = await db.Configurations.AsNoTracking().SelectSpecificRevision(revisionToExport);
            if (config == null) throw new Exception($"Failed to load requested config revision: {revisionToExport}");
            return await GenerateExportAsync(config, exportOptions, cancellationToken);
        }

        private async Task<ProxyExport> GenerateExportAsync(ProxyConfig configToExport, ExportOptions exportOptions, CancellationToken cancellationToken = default) {
            var export = new ProxyExportVLatest();
            if (exportOptions.HasFlag(ExportOptions.IncludeSystemSettings)) {
                export.SystemSettings = configToExport
                    .SystemSettingsToExport(exportOptions.HasFlag(ExportOptions.IncludeSystemCertificates))
                    .FromCurrent();
            }
            if (exportOptions.HasFlag(ExportOptions.IncludeRoutesAndClusters)) {
                export.Routes = configToExport.Routes.Select(_ => _.ToExport().FromCurrent()).ToList();
                export.Clusters = configToExport.Clusters.Select(_ => _.ToExport().FromCurrent()).ToList();
            }
            if (exportOptions.HasFlag(ExportOptions.IncludeSniMaps)) {
                export.SniMaps = configToExport.SniMaps.Select(_ => _.ToExport().FromCurrent()).ToList();
            }

            if (exportOptions.HasFlag(ExportOptions.IncludeAllCertificates)) {
                await CollectAllCertificatesAsync(export, cancellationToken);
            } else {
                await CollectReferencedCertificatesAsync(export, cancellationToken);
            }

            return export;
        }

        private async Task CollectAllCertificatesAsync(ProxyExportVLatest export, CancellationToken cancellationToken = default) {
            using var db = _dbFactory.CreateDbContext();
            var accounts = await db.AcmeAccounts.AsNoTracking().ToListAsync(cancellationToken);
            if (accounts.Count > 0) {
                var accountExports = new List<AcmeAccountExport>();
                foreach (var account in accounts) {
                    accountExports.Add(await account.ToExportAsync(cancellationToken));
                }
                export.AcmeAccounts = accountExports.Select(_ => _.FromCurrent()).ToList();
            }

            var dnsProviders = await db.AcmeDnsProviders.AsNoTracking().ToListAsync(cancellationToken);
            if (dnsProviders.Count > 0) export.AcmeDnsProviders = dnsProviders.Select(_ => _.ToExport().FromCurrent()).ToList();

            var certAuthorities = await db.CertificateAuthorities.AsNoTracking().ToListAsync(cancellationToken);
            if (certAuthorities.Count > 0) {
                var caExports = new List<CertificateAuthorityExport>();
                foreach (var ca in certAuthorities) {
                    caExports.Add(await ca.ToExportAsync(cancellationToken));
                }
                export.CertificateAuthorities = caExports.Select(_ => _.FromCurrent()).ToList();
            }

            var serverCerts = await db.ServerCerts.AsNoTracking()
                    .Include(cert => ((IssuedCert)cert).IssuingCA)
                    .Include(cert => ((AcmeCert)cert).AcmeAccount)
                    .Include(cert => ((AcmeCert)cert).Hosts)
                        .ThenInclude(host => ((AcmeDnsCertHost)host).DnsProvider)
                    .ToListAsync(cancellationToken); ;
            if (serverCerts.Count > 0) {
                var certExports = new List<ServerCertExport>();
                foreach (var cert in serverCerts) {
                    certExports.Add(await cert.ToExportAsync(cancellationToken));
                }
                export.ServerCertificates = certExports.Select(_ => _.FromCurrent()).ToList();
            }
        }

        private async Task CollectReferencedCertificatesAsync(ProxyExportVLatest export, CancellationToken cancellationToken = default) {
            var serverCertIds = new HashSet<Guid>();

            if (export.SystemSettings?.AdminCertId != null) {
                serverCertIds.Add(export.SystemSettings.AdminCertId.Value);
                if (export.SystemSettings.FallbackCertId != null) serverCertIds.Add(export.SystemSettings.FallbackCertId.Value);
            }

            if (export.SniMaps != null && export.SniMaps.Count > 0) {
                export.SniMaps.ForEach(_ => serverCertIds.Add(_.CertificateId));
            }

            if (serverCertIds.Count > 0) {
                var acmeAccountIds = new HashSet<Guid>();
                var dnsProviderIds = new HashSet<Guid>();
                var certAuthorityIds = new HashSet<Guid>();

                export.ServerCertificates = new();
                using var db = _dbFactory.CreateDbContext();
                var certs = await db.ServerCerts
                    .AsNoTracking()
                    .Include(cert => ((IssuedCert)cert).IssuingCA)
                    .Include(cert => ((AcmeCert)cert).AcmeAccount)
                    .Include(cert => ((AcmeCert)cert).Hosts)
                        .ThenInclude(host => ((AcmeDnsCertHost)host).DnsProvider)
                    .Where(cert => serverCertIds.Contains(cert.Id))
                    .ToListAsync(cancellationToken);

                foreach (var cert in certs) {
                    switch (cert) {
                        case IssuedCert issued:
                            if (certAuthorityIds.Add(issued.IssuingCAId)) {
                                if (export.CertificateAuthorities == null) export.CertificateAuthorities = new();
                                export.CertificateAuthorities.Add((await issued.IssuingCA.ToExportAsync(cancellationToken)).FromCurrent());
                            }
                            export.ServerCertificates.Add((await issued.ToExportAsync(cancellationToken)).FromCurrent());
                            break;
                        case AcmeCert acme:
                            if (acmeAccountIds.Add(acme.AcmeAccountId)) {
                                if (export.AcmeAccounts == null) export.AcmeAccounts = new();
                                export.AcmeAccounts.Add((await acme.AcmeAccount.ToExportAsync(cancellationToken)).FromCurrent());
                            }
                            foreach (var dnsHost in acme.Hosts.OfType<AcmeDnsCertHost>()) {
                                if (dnsProviderIds.Add(dnsHost.DnsProviderId)) {
                                    if (export.AcmeDnsProviders == null) export.AcmeDnsProviders = new();
                                    export.AcmeDnsProviders.Add(dnsHost.DnsProvider.ToExport().FromCurrent());
                                }
                            }
                            export.ServerCertificates.Add((await acme.ToExportAsync(cancellationToken)).FromCurrent());
                            break;
                        case ImportedCert imported:
                            export.ServerCertificates.Add((await imported.ToExportAsync(cancellationToken)).FromCurrent());
                            break;
                    }
                }
            }
        }

        public ProxyExportVLatest UpgradeExportToLatest(ProxyExport export) {
            while (true) {
                if (export is ProxyExportVLatest latest) return latest;
                export = export.ToNext();
            }
        }

        public async Task<ProxyConfig> RealizeExportAsync(ProxyExport export, ExportOptions includedAreas, ImportMergeOptions mergeOptions, CancellationToken cancellationToken = default) {
            var latest = UpgradeExportToLatest(export);
            return await RealizeLatestAsync(latest, includedAreas, mergeOptions, cancellationToken);
        }

        private async Task<ProxyConfig> RealizeLatestAsync(ProxyExportVLatest export, ExportOptions includedAreas, ImportMergeOptions mergeOptions, CancellationToken cancellationToken = default) {
            int revision = _proxyState.CandidateConfigRevision;

            using var db = _dbFactory.CreateDbContext();
            var currentConfig = await db.Configurations.SelectSpecificRevision(_proxyState.RunningConfig.Revision);
            if (currentConfig == null) throw new Exception("Failed to load current config.");

            var config = new ProxyConfig {
                Revision = revision,
                BasedOnRevision = currentConfig.Revision,
                Committed = false,
                Confirmed = false,
                Reverted = false,
                ReversionReason = null,
                CommittedAt = null,
                ConfirmSeconds = 0
            };

            if (export.SystemSettings != null && includedAreas.HasFlag(ExportOptions.IncludeSystemSettings)) {
                var systemSettings = export.SystemSettings.ToCurrent();
                if (mergeOptions.HasFlag(ImportMergeOptions.SkipAdminPortAndListen)) {
                    config.AdminListenAny = currentConfig.AdminListenAny;
                    config.AdminPort = currentConfig.AdminPort;
                } else {
                    config.AdminListenAny = systemSettings.AdminListenAny;
                    config.AdminPort = systemSettings.AdminPort;
                }
                config.MaxRequestBodySize = systemSettings.MaxRequestBodySize;
                config.UnmatchedResponse = systemSettings.UnmatchedResponse.Realize();
                if (systemSettings.AdminCertId != null && includedAreas.HasFlag(ExportOptions.IncludeSystemCertificates)) {
                    config.AdminCertId = systemSettings.AdminCertId.Value;
                    config.FallbackCertId = systemSettings.FallbackCertId;
                } else {
                    config.AdminCertId = currentConfig.AdminCertId;
                    config.FallbackCertId = currentConfig.FallbackCertId;
                }
                config.TrustedProxies = systemSettings.TrustedProxies?.ToList() ??  new();
                config.TrustedProxyChainLimit = systemSettings.TrustedProxyChainLimit ?? 0;
            } else {
                config.AdminListenAny = currentConfig.AdminListenAny;
                config.AdminPort = currentConfig.AdminPort;
                config.MaxRequestBodySize = currentConfig.MaxRequestBodySize;
                config.UnmatchedResponse = currentConfig.UnmatchedResponse.Clone();
                config.AdminCertId = currentConfig.AdminCertId;
                config.FallbackCertId = currentConfig.FallbackCertId;
                config.TrustedProxies = currentConfig.TrustedProxies.ToList();
                config.TrustedProxyChainLimit = currentConfig.TrustedProxyChainLimit;
            }

            if (export.Routes != null && includedAreas.HasFlag(ExportOptions.IncludeRoutesAndClusters)) {
                if (mergeOptions.HasFlag(ImportMergeOptions.MergeClustersAndRoutes)) {
                    config.Routes = currentConfig.Routes.Select(_ => _.Clone(revision)).ToList();
                    foreach (var route in export.Routes) {
                        config.Routes.RemoveAll(_ => _.Id == route.Id);
                        config.Routes.Add(route.ToCurrent().Realize(revision));
                    }
                } else {
                    config.Routes = export.Routes.Select(_ => _.ToCurrent().Realize(revision)).ToList();
                }
            } else {
                config.Routes = currentConfig.Routes.Select(_ => _.Clone(revision)).ToList();
            }

            if (export.Clusters != null && includedAreas.HasFlag(ExportOptions.IncludeRoutesAndClusters)) {
                if (mergeOptions.HasFlag(ImportMergeOptions.MergeClustersAndRoutes)) {
                    config.Clusters = currentConfig.Clusters.Select(_ => _.Clone(revision)).ToList();
                    foreach (var cluster in export.Clusters) {
                        config.Clusters.RemoveAll(_ => _.Id == cluster.Id);
                        config.Clusters.Add(cluster.ToCurrent().Realize(revision));
                    }
                } else {
                    config.Clusters = export.Clusters.Select(_ => _.ToCurrent().Realize(revision)).ToList();
                }
            } else {
                config.Clusters = currentConfig.Clusters.Select(_ => _.Clone(revision)).ToList();
            }

            if (export.SniMaps != null && includedAreas.HasFlag(ExportOptions.IncludeSniMaps)) {
                if (mergeOptions.HasFlag(ImportMergeOptions.MergeSniMaps)) {
                    config.SniMaps = currentConfig.SniMaps.Select(_ => _.Clone(revision)).ToList();
                    foreach (var map in export.SniMaps) {
                        config.SniMaps.RemoveAll(_ => _.Host == map.Host);
                        config.SniMaps.Add(map.ToCurrent().Realize(revision));
                    }
                } else {
                    config.SniMaps = export.SniMaps.Select(_ => _.ToCurrent().Realize(revision)).ToList();
                }
            } else {
                config.SniMaps = currentConfig.SniMaps.Select(_ => _.Clone(revision)).ToList();
            }

            return config;
        }

        public async Task ProcessImport(ProxyExport export, ExportOptions includedAreas, ImportMergeOptions mergeOptions, IProgress<(string Step, string? Detail)>? progress = null, CancellationToken cancellationToken = default) {
            progress?.Report(("RestoreStep_ProcessingFile", null));
            var latest = UpgradeExportToLatest(export);
            var newConfig = await RealizeLatestAsync(latest, includedAreas, mergeOptions, cancellationToken);

            progress?.Report(("RestoreStep_ImportingCertificateData", null));
            if (includedAreas.HasFlag(ExportOptions.IncludeAllCertificates)) {
                await ImportAllCertificateData(latest, mergeOptions);
            } else if (latest.ServerCertificates != null && latest.ServerCertificates.Count > 0) {
                await ImportReferencedCertificateData(latest, includedAreas, mergeOptions);
            }

            progress?.Report(("RestoreStep_PromotingCandidateConfig", null));
            await _config.PromoteImportedConfigToCandidate(newConfig);
        }

        private async Task ImportAllCertificateData(ProxyExportVLatest latest, ImportMergeOptions mergeOptions) {
            using var db = _dbFactory.CreateDbContext();

            if (latest.CertificateAuthorities != null) {
                foreach (var ca in latest.CertificateAuthorities) {
                    await ImportOrUpdateCa(db, ca);
                }
            }

            if (latest.AcmeDnsProviders != null) {
                foreach (var provider in latest.AcmeDnsProviders) {
                    await ImportOrUpdateAcmeDnsProvider(db, provider, mergeOptions);
                }
            }

            if (latest.AcmeAccounts != null) {
                foreach (var account in latest.AcmeAccounts) {
                    await ImportOrUpdateAcmeAccount(db, account);
                }
            }

            if (latest.ServerCertificates != null) {
                foreach (var cert in latest.ServerCertificates) {
                    await ImportOrUpdateCertificate(db, cert, mergeOptions);
                }
            }
        }

        private async Task ImportReferencedCertificateData(ProxyExportVLatest latest, ExportOptions includedAreas, ImportMergeOptions mergeOptions) {
            using var db = _dbFactory.CreateDbContext();
            var referencedCerts = new List<ServerCertExportVLatest>();

            if (includedAreas.HasFlag(ExportOptions.IncludeSystemCertificates)) {
                if (latest.SystemSettings?.AdminCertId != null) {
                    referencedCerts.Add(
                        latest.ServerCertificates!.Single(_ => _.Id == latest.SystemSettings.AdminCertId));
                }
                if (latest.SystemSettings?.FallbackCertId != null) {
                    referencedCerts.Add(
                        latest.ServerCertificates!.Single(_ => _.Id == latest.SystemSettings.FallbackCertId));
                }
            }

            if (includedAreas.HasFlag(ExportOptions.IncludeSniMaps) && latest.SniMaps != null) {
                foreach (var map in latest.SniMaps) {
                    referencedCerts.Add(
                        latest.ServerCertificates!.Single(_ => _.Id == map.CertificateId));
                }
            }

            foreach (var cert in referencedCerts) {
                switch (cert) {
                    case ImportedCertExportVLatest imported:
                        await ImportOrUpdateCertificate(db, imported, mergeOptions);
                        break;

                    case IssuedCertExportVLatest issued:
                        await ImportOrUpdateCa(db, latest.CertificateAuthorities!.Single(_ => _.Id == issued.IssuingCAId));
                        await ImportOrUpdateCertificate(db, issued, mergeOptions);
                        break;

                    case AcmeCertExportVLatest acme:
                        await ImportOrUpdateAcmeAccount(db, latest.AcmeAccounts!.Single(_ => _.Id == acme.AcmeAccountId));

                        foreach (var dnsHost in acme.Hosts.OfType<AcmeDnsCertHostExportVLatest>()) {
                            await ImportOrUpdateAcmeDnsProvider(db, latest.AcmeDnsProviders!.Single(_ => _.Id == dnsHost.DnsProviderId), mergeOptions);
                        }

                        await ImportOrUpdateCertificate(db, acme, mergeOptions);
                        break;
                }
            }
        }

        private async Task ImportOrUpdateCa(ProxyDb db, CertificateAuthorityExportVLatest ca) {
            var existingCa = await db.CertificateAuthorities.FirstOrDefaultAsync(_ => _.Id == ca.Id);
            if (existingCa != null) {
                existingCa.Name = ca.Name;
                existingCa.Hidden = ca.Hidden;
            } else {
                db.CertificateAuthorities.Add(new CACert {
                    Id = ca.Id,
                    Name = ca.Name,
                    Hidden = ca.Hidden
                });
            }
            await File.WriteAllBytesAsync(Certificates.GetCertFilePath(ca.Id), Convert.FromBase64String(ca.Material));
            await db.SaveChangesAsync();
        }

        private async Task ImportOrUpdateAcmeAccount(ProxyDb db, AcmeAccountExportVLatest account) {
            var existingAccount = await db.AcmeAccounts.FirstOrDefaultAsync(_ => _.Id == account.Id);
            if (existingAccount != null) {
                existingAccount.Name = account.Name;
            } else {
                db.AcmeAccounts.Add(new AcmeAccount {
                    Id = account.Id,
                    Name = account.Name,
                    Hidden = account.Hidden,
                    Provider = account.Provider,
                    Url = account.Url
                });
                await File.WriteAllBytesAsync(AcmeClientService.GetKeyFilePath(account.Id), Convert.FromBase64String(account.Key));
            }
            await db.SaveChangesAsync();
        }

        private async Task ImportOrUpdateAcmeDnsProvider(ProxyDb db, AcmeDnsProviderExportVLatest provider, ImportMergeOptions mergeOptions) {
            var existingProvider = await db.AcmeDnsProviders.FirstOrDefaultAsync(_ => _.Id == provider.Id);
            if (existingProvider != null) {
                existingProvider.Name = provider.Name;
                if (mergeOptions.HasFlag(ImportMergeOptions.UpdateDnsScriptsOnConflict)) {
                    existingProvider.Script = provider.Script;
                    existingProvider.Draft = null;
                }
            } else {
                db.AcmeDnsProviders.Add(new AcmeDnsProvider {
                    Id = provider.Id,
                    Name = provider.Name,
                    Script = provider.Script,
                });
            }
            await db.SaveChangesAsync();
        }

        private async Task ImportOrUpdateCertificate(ProxyDb db, ServerCertExportVLatest cert, ImportMergeOptions mergeOptions) {
            var existingCert = await db.ServerCerts.FirstOrDefaultAsync(_ => _.Id == cert.Id);
            if (existingCert != null) {
                existingCert.Name = cert.Name;
                if (existingCert is AcmeCert) {
                    if (!mergeOptions.HasFlag(ImportMergeOptions.IgnoreOnAcmeConflict)) {
                        var existing = _certService.GetCertificateContext(existingCert.Id).TargetCertificate;
                        var imported = Convert.FromBase64String(cert.Material);
                        using (var leaf = Certificates.GetLeafCertFromPfxBytes(imported)) {
                            if (leaf.NotAfter > existing.NotAfter) {
                                await File.WriteAllBytesAsync(Certificates.GetCertFilePath(cert.Id), imported);
                            }
                        }
                    }
                } else {
                    await File.WriteAllBytesAsync(Certificates.GetCertFilePath(cert.Id), Convert.FromBase64String(cert.Material));
                }
            } else {
                switch (cert) {
                    case ImportedCertExportVLatest imported:
                        db.ImportedCerts.Add(new ImportedCert {
                            Id = imported.Id,
                            Name = imported.Name,
                            Hidden = imported.Hidden
                        });
                        break;

                    case IssuedCertExportVLatest issued:
                        db.IssuedCerts.Add(new IssuedCert {
                            Id = issued.Id,
                            Name = issued.Name,
                            Hidden = issued.Hidden,
                            IssuingCAId = issued.IssuingCAId
                        });
                        break;

                    case AcmeCertExportVLatest acme:
                        var newAcme = new AcmeCert {
                            Id = acme.Id,
                            Name = acme.Name,
                            Hidden = acme.Hidden,
                            AcmeAccountId = acme.AcmeAccountId,
                            Hosts = new()
                        };
                        foreach (var host in acme.Hosts) {
                            switch (host) {
                                case AcmeHttpCertHostExportVLatest http:
                                    newAcme.Hosts.Add(new AcmeHttpCertHost {
                                        CertificateId = acme.Id,
                                        Host = http.Host
                                    });
                                    break;

                                case AcmeDnsCertHostExportVLatest dns:
                                    newAcme.Hosts.Add(new AcmeDnsCertHost {
                                        CertificateId = acme.Id,
                                        Host = dns.Host,
                                        DnsProviderId = dns.DnsProviderId,
                                    });
                                    break;
                            }
                        }
                        db.AcmeCerts.Add(newAcme);
                        break;
                }
                await File.WriteAllBytesAsync(Certificates.GetCertFilePath(cert.Id), Convert.FromBase64String(cert.Material));
            }
            await db.SaveChangesAsync();
        }

        public ProxyExport DecryptExport(byte[] fileBytes, string password) {
            int newline = Array.IndexOf(fileBytes, (byte)0x0A);
            if (newline < 0) throw new InvalidDataException("Header is not newline-terminated.");

            var headerBytes = fileBytes.AsMemory(0, newline);
            var payload = fileBytes.AsSpan(newline + 1);

            using var headerDoc = JsonDocument.Parse(headerBytes.ToArray());
            string? version = headerDoc.RootElement.GetProperty("Version").GetString();

            ProxyExport export = version switch {
                "1.25" => DecryptExportV1_25(headerBytes.Span, payload, password),
                "1.24" => DecryptExportV1_24(headerBytes.Span, payload, password),
                "1.20" => DecryptExportV1_20(headerBytes.Span, payload, password),
                _ => throw new NotSupportedException($"Unsupported encryption version: {version}")
            };

            return UpgradeExportToLatest(export);
        }
    }

    public static class ImportExportHelpers {
        public static bool HasSystemSettings(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return latest.SystemSettings != null;
        }

        public static bool HasSystemCertificates(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return latest.SystemSettings?.AdminCertId != null;
        }

        public static bool HasRoutesAndClusters(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return latest.Routes != null || latest.Clusters != null;
        }

        public static bool HasSniMaps(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return latest.SniMaps != null;
        }

        public static bool HasAcmeDnsProviders(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return latest.AcmeDnsProviders?.Count > 0;
        }

        public static bool HasCertificates(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return latest.ServerCertificates?.Count > 0 || latest.CertificateAuthorities?.Count > 0;
        }

        public static bool HasAcmeCertificates(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return latest.ServerCertificates?.OfType<AcmeCertExportVLatest>().Any() ?? false;
        }

        public static Dictionary<string, List<string>> GetSummary(this ProxyExport export) {
            if (export is not ProxyExportVLatest latest) throw new Exception("Upgrade export to latest version first.");
            return new Dictionary<string, List<string>> {
                { "Routes", latest.Routes?.Select(_ => _.Name).ToList() ?? [] },
                { "Clusters", latest.Clusters?.Select(_ => _.Name).ToList() ?? [] },
                { "SNI Maps", latest.SniMaps?.Select(_ => _.Host).ToList() ?? [] },
                { "ACME Accounts", latest.AcmeAccounts?.Select(_ => _.Name).ToList() ?? [] },
                { "ACME DNS Providers", latest.AcmeDnsProviders?.Select(_ => _.Name).ToList() ?? [] },
                { "ACME Certificates", latest.ServerCertificates?.OfType<AcmeCertExportVLatest>().Select(_ => _.Name).ToList() ?? [] },
                { "Certificate Authorities", latest.CertificateAuthorities?.Select(_ => _.Name).ToList() ?? [] },
                { "CA Issued Certificates", latest.ServerCertificates?.OfType<IssuedCertExportVLatest>().Select(_ => _.Name).ToList() ?? [] },
                { "Imported Certificates", latest.ServerCertificates?.OfType<ImportedCertExportVLatest>().Select(_ => _.Name).ToList() ?? [] }
            };
        }

        public static SystemSettingsExportVLatest FromCurrent(this SystemSettingsExport systemExport) {
            return new SystemSettingsExportVLatest {
                AdminListenAny = systemExport.AdminListenAny,
                AdminPort = systemExport.AdminPort,
                AdminCertId = systemExport.AdminCertId,
                FallbackCertId = systemExport.FallbackCertId,
                MaxRequestBodySize = systemExport.MaxRequestBodySize,
                UnmatchedResponse = systemExport.UnmatchedResponse.FromCurrent(),
                TrustedProxies = systemExport.TrustedProxies?.ToList(),
                TrustedProxyChainLimit = systemExport.TrustedProxyChainLimit,
            };
        }

        public static RouteExportVLatest FromCurrent(this RouteExport routeExport) {
            return new RouteExportVLatest {
                Id = routeExport.Id,
                Name = routeExport.Name,
                Description = routeExport.Description,
                Disabled = routeExport.Disabled,
                Order = routeExport.Order,
                ResponseType = routeExport.ResponseType,
                ClusterId = routeExport.ClusterId,
                StaticResponse = routeExport.StaticResponse?.FromCurrent(),
                Methods = routeExport.Methods?.ToList(),
                Hosts = routeExport.Hosts?.ToList(),
                MatchCriteria = routeExport.MatchCriteria.Select(_ => _.FromCurrent()).ToList(),
                PreserveHostHeader = routeExport.PreserveHostHeader,
                PreserveClientHeaders = routeExport.PreserveClientHeaders,
                AllowedRequestHeaders = routeExport.AllowedRequestHeaders?.ToList(),
                XForwardedPrefix = routeExport.XForwardedPrefix,
                XForwardedForAction = routeExport.XForwardedForAction,
                XForwardedProtoAction = routeExport.XForwardedProtoAction,
                XForwardedHostAction = routeExport.XForwardedHostAction,
                XForwardedPrefixAction = routeExport.XForwardedPrefixAction,
                Path = routeExport.Path,
                PathIsPrefix = routeExport.PathIsPrefix,
                HttpPort = routeExport.HttpPort,
                HttpsPort = routeExport.HttpsPort,
                SuppressHttpsRedirect = routeExport.SuppressHttpsRedirect,
                MaxRequestBodySize = routeExport.MaxRequestBodySize,
                RequestTransforms = routeExport.RequestTransforms.Select(_ => _.FromCurrent()).ToList(),
                PreserveResponseHeaders = routeExport.PreserveResponseHeaders,
                AllowedResponseHeaders = routeExport.AllowedResponseHeaders?.ToList(),
                ResponseTransforms = routeExport.ResponseTransforms.Select(_ => _.FromCurrent()).ToList(),
                IpRestrictionMode = routeExport.IpRestrictionMode,
                IpRestrictionEntries = routeExport.IpRestrictionEntries?.ToList(),
                IpDenyResponse = routeExport.IpDenyResponse?.FromCurrent(),
            };
        }

        public static RequestTransformExportVLatest FromCurrent(this RequestTransformExport transformExport) {
            return transformExport switch {
                RequestPathTransformExport pathTransform => new RequestPathTransformExportVLatest {
                    Id = pathTransform.Id,
                    RouteId = pathTransform.RouteId,
                    Mode = pathTransform.Mode,
                    PathString = pathTransform.PathString
                },
                RequestMethodTransformExport methodTransform => new RequestMethodTransformExportVLatest {
                    Id = methodTransform.Id,
                    RouteId = methodTransform.RouteId,
                    FromHttpMethod = methodTransform.FromHttpMethod,
                    ToHttpMethod = methodTransform.ToHttpMethod
                },
                RequestHeaderTransformExport headerTrasnform => new RequestHeaderTransformExportVLatest {
                    Id = headerTrasnform.Id,
                    RouteId = headerTrasnform.RouteId,
                    Operation = headerTrasnform.Operation,
                    HeaderName = headerTrasnform.HeaderName,
                    HeaderValue = headerTrasnform.HeaderValue
                },
                RequestQueryTransformExport queryTransform => new RequestQueryTransformExportVLatest {
                    Id = queryTransform.Id,
                    RouteId = queryTransform.RouteId,
                    Operation = queryTransform.Operation,
                    QueryKey = queryTransform.QueryKey,
                    QueryValue = queryTransform.QueryValue
                },
                _ => throw new NotSupportedException($"No export mapping for {transformExport.GetType().Name}")
            };
        }

        public static ResponseTransformExportVLatest FromCurrent(this ResponseTransformExport transformExport) {
            return transformExport switch {
                ResponseHeaderTransformExport headerTransform => new ResponseHeaderTransformExportVLatest {
                    Id = headerTransform.Id,
                    RouteId = headerTransform.RouteId,
                    Operation = headerTransform.Operation,
                    HeaderName = headerTransform.HeaderName,
                    HeaderValue = headerTransform.HeaderValue
                },
                _ => throw new NotSupportedException($"No export mapping for {transformExport.GetType().Name}")
            };
        }

        public static RouteMatchExportVLatest FromCurrent(this RouteMatchExport matchExport) {
            return matchExport switch {
                RouteHeaderMatchExport headerMatch => new RouteHeaderMatchExportVLatest {
                    Id = headerMatch.Id,
                    RouteId = headerMatch.RouteId,
                    Key = headerMatch.Key,
                    Values = headerMatch.Values?.ToList(),
                    CaseSensitiveValues = headerMatch.CaseSensitiveValues,
                    Mode = headerMatch.Mode
                },
                RouteQueryMatchExport queryMatch => new RouteQueryMatchExportVLatest {
                    Id = queryMatch.Id,
                    RouteId = queryMatch.RouteId,
                    Key = queryMatch.Key,
                    Values = queryMatch.Values?.ToList(),
                    CaseSensitiveValues = queryMatch.CaseSensitiveValues,
                    Mode = queryMatch.Mode
                },
                _ => throw new NotSupportedException($"No export mapping for {matchExport.GetType().Name}")
            };
        }

        public static StaticResponseExportVLatest FromCurrent(this StaticResponseExport responseExport) {
            return new StaticResponseExportVLatest {
                HttpStatusCode = responseExport.HttpStatusCode,
                UseTarpit = responseExport.UseTarpit,
                Body = responseExport.Body,
                Headers = responseExport.Headers?.ToDictionary()
            };
        }

        public static ClusterExportVLatest FromCurrent(this ClusterExport clusterExport) {
            return new ClusterExportVLatest {
                Id = clusterExport.Id,
                Name = clusterExport.Name,
                Description = clusterExport.Description,
                Destinations = clusterExport.Destinations.Select(_ => _.FromCurrent()).ToList(),
                LoadBalancing = clusterExport.LoadBalancing,
                StickySessions = clusterExport.StickySessions,
                AcceptAnyCert = clusterExport.AcceptAnyCert
            };
        }

        public static DestinationExportVLatest FromCurrent(this DestinationExport destinationExport) {
            return new DestinationExportVLatest {
                Id = destinationExport.Id,
                Name = destinationExport.Name,
                ClusterId = destinationExport.ClusterId,
                Order = destinationExport.Order,
                Address = destinationExport.Address,
                Health = destinationExport.Health
            };
        }

        public static SniMapExportVLatest FromCurrent(this SniMapExport mapExport) {
            return new SniMapExportVLatest {
                Host = mapExport.Host,
                CertificateId = mapExport.CertificateId
            };
        }

        public static AcmeAccountExportVLatest FromCurrent(this AcmeAccountExport accountExport) {
            return new AcmeAccountExportVLatest {
                Id = accountExport.Id,
                Name = accountExport.Name,
                Provider = accountExport.Provider,
                Url = accountExport.Url,
                Hidden = accountExport.Hidden,
                Key = accountExport.Key
            };
        }

        public static AcmeDnsProviderExportVLatest FromCurrent(this AcmeDnsProviderExport dnsProviderExport) {
            return new AcmeDnsProviderExportVLatest {
                Id = dnsProviderExport.Id,
                Name = dnsProviderExport.Name,
                Script = dnsProviderExport.Script
            };
        }

        public static CertificateAuthorityExportVLatest FromCurrent(this CertificateAuthorityExport caExport) {
            return new CertificateAuthorityExportVLatest {
                Id = caExport.Id,
                Name = caExport.Name,
                Hidden = caExport.Hidden,
                Material = caExport.Material
            };
        }

        public static ServerCertExportVLatest FromCurrent(this ServerCertExport certExport) {
            return certExport switch {
                IssuedCertExport issuedCert => new IssuedCertExportVLatest {
                    Id = issuedCert.Id,
                    Name = issuedCert.Name,
                    Hidden = issuedCert.Hidden,
                    IssuingCAId = issuedCert.IssuingCAId,
                    Material = issuedCert.Material
                },
                AcmeCertExport acmeCert => new AcmeCertExportVLatest {
                    Id = acmeCert.Id,
                    Name = acmeCert.Name,
                    Hidden = acmeCert.Hidden,
                    AcmeAccountId = acmeCert.AcmeAccountId,
                    LastAttempt = acmeCert.LastAttempt,
                    Hosts = acmeCert.Hosts.Select(h => h.FromCurrent()).ToList(),
                    Material = acmeCert.Material
                },
                ImportedCertExport importedCert => new ImportedCertExportVLatest {
                    Id = importedCert.Id,
                    Name = importedCert.Name,
                    Hidden = importedCert.Hidden,
                    Material = importedCert.Material
                },
                _ => throw new NotSupportedException($"No export mapping for {certExport.GetType().Name}")
            };
        }

        public static AcmeCertHostExportVLatest FromCurrent(this AcmeCertHostExport hostExport) {
            return hostExport switch {
                AcmeHttpCertHostExport httpHost => new AcmeHttpCertHostExportVLatest {
                    Host = httpHost.Host
                },
                AcmeDnsCertHostExport dnsHost => new AcmeDnsCertHostExportVLatest {
                    Host = dnsHost.Host,
                    DnsProviderId = dnsHost.DnsProviderId
                },
                _ => throw new NotSupportedException($"No export mapping for {hostExport.GetType().Name}")
            };
        }
    }

    [Flags]
    public enum ExportOptions {
        None = 0,
        IncludeSystemSettings = 1,
        IncludeSystemCertificates = 2,
        IncludeRoutesAndClusters = 4,
        IncludeSniMaps = 8,
        IncludeAllCertificates = 16,

        Sensitive = IncludeAllCertificates
            | IncludeSniMaps
            | IncludeSystemCertificates,

        FullExport = IncludeSystemSettings
            | IncludeSystemCertificates
            | IncludeRoutesAndClusters
            | IncludeSniMaps
            | IncludeAllCertificates,

        ImportAll = IncludeSystemSettings
            | IncludeSystemCertificates
            | IncludeRoutesAndClusters
            | IncludeSniMaps
            | IncludeAllCertificates
    }

    [Flags]
    public enum ImportMergeOptions {
        MergeNone = 0,
        SkipAdminPortAndListen = 1,
        MergeClustersAndRoutes = 2,
        MergeSniMaps = 4,
        IgnoreOnAcmeConflict = 8,
        UpdateDnsScriptsOnConflict = 16,
    }
}
