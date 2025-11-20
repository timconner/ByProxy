namespace ByProxy.AdminApp.Pages.SystemSettings;
public partial class SystemSettingsIndex : IDisposable {
    private ProxyDb _db = default!;
    private ProxyConfig? _proxyConfig;

    private string? _version;

    protected override async Task OnInitializedAsync() {
        var version = typeof(Program).Assembly.GetName().Version!;
        _version = $"{version.Major}.{version.Minor}.{version.Build}";

        _db = _dbFactory.CreateDbContext();

        _proxyConfig = await _db.Configurations
            .IgnoreAutoIncludes()
            .Include(_ => _.AdminCert)
            .Include(_ => _.FallbackCert)
            .Where(_ => _.Revision == _proxyState.CandidateConfigRevision)
            .FirstAsync();

    }

    private async Task SaveChanges() {
        await _modal.PerformSave(async () => {
            await _db.SaveChangesAsync();
        });
        _ = _configService.UpdateHasChangesPending();
    }

    private async Task TarpitToggled(bool tarpitEnabled) {
        if (_proxyConfig == null) return;
        if (tarpitEnabled) {
            _proxyConfig.Tarpit = true;
            _proxyConfig.UnmatchedStatus = 500;
            await SaveChanges();
            await _modal.OkOnly("Tarpit", _strings["WARN_EnabledTarpit"]);
        } else {
            _proxyConfig.Tarpit = false;
            _proxyConfig.UnmatchedStatus = 404;
            await SaveChanges();
        }
    }

    private async Task FallbackCertToggled(bool useFallbackCert) {
        if (_proxyConfig == null) return;
        if (useFallbackCert) {
            _proxyConfig.FallbackCertId = _proxyConfig.AdminCertId;
            _proxyConfig.FallbackCert = _proxyConfig.AdminCert;
        } else {
            if (_proxyConfig.Tarpit) {
                var result = await _modal.OkCancel("Tarpit", _strings["WARN_RefusingConnectionsTarpit"]);
                if (result != DialogResult.OK) return;
            }
            _proxyConfig.FallbackCertId = null;
            _proxyConfig.FallbackCert = null;
        }
        await SaveChanges();
    }

    private async Task ViewFallbackCertDetails() {
        if (_proxyConfig?.FallbackCert == null) return;
        await _modal.CertificateDetails(_proxyConfig.FallbackCert.Id);
    }

    private async Task ChangeFallbackCert() {
        var result = await _modal.CertificatePicker(_strings["Fallback Certificate"]);
        if (result.DialogResult != DialogResult.OK || result.Value == null) return;
        await _modal.PerformProcessing(async () => {
            var cert = await _db.ServerCerts.FindAsync(result.Value.Id);
            if (cert == null) throw new Exception("Certificate not found in database.");
            _proxyConfig!.FallbackCertId = cert.Id;
            _proxyConfig!.FallbackCert = cert;
        });
        await SaveChanges();
    }

    private async Task ViewAdminCertDetails() {
        if (_proxyConfig?.AdminCert == null) return;
        await _modal.CertificateDetails(_proxyConfig.AdminCert.Id);
    }

    private async Task ChangeAdminCert() {
        var result = await _modal.CertificatePicker(_strings["HTTPS Certificate"]);
        if (result.DialogResult != DialogResult.OK || result.Value == null) return;
        await _modal.PerformProcessing(async () => {
            var cert = await _db.ServerCerts.FindAsync(result.Value.Id);
            if (cert == null) throw new Exception("Certificate not found in database.");
            _proxyConfig!.AdminCertId = cert.Id;
            _proxyConfig!.AdminCert = cert;
        });
        await SaveChanges();
    }

    private async Task RestartProxy() {
        var result = await _modal.YesNo(_strings["Restart Proxy Server"], _strings["WARN_RestartProxy"]);
        if (result != DialogResult.Yes) return;

        _modal.StartLoad(_strings["Restart Proxy Server"]);
        await Task.Delay(1); // Force UI Refresh

        _proxyState.RequestKestrelRestart();
    }

    private async Task ShutdownProxy() {
        var result = await _modal.YesNo(_strings["Shutdown Proxy Server"], _strings["WARN_ShutdownProxy"]);
        if (result != DialogResult.Yes) return;

        _modal.StartLoad(_strings["Shutdown Proxy Server"]);
        await Task.Delay(1); // Force UI Refresh

        _lifetime.StopApplication();
    }

    private async Task UpdateAdminPort(int newPort) {
        if (newPort < 1 || newPort > 65535) {
            await _modal.OkOnly(_strings["PortOutOfRange"], _strings["ERR_PortOutOfRange"]);
            return;
        }
        bool portInUse = false;
        if (!await _modal.PerformProcessing(async () => {
            portInUse = await _db.Routes
                .AsNoTracking()
                .Where(_ => _.ConfigRevision == _proxyState.CandidateConfigRevision)
                .AnyAsync(_ => _.HttpPort == newPort || _.HttpsPort == newPort);
        })) return;

        if (portInUse) {
            await _modal.OkOnly(_strings["PortInUse"], _strings["ERR_AdminPortInUse"]);
            return;
        }

        _proxyConfig!.AdminPort = newPort;
        await SaveChanges();
    }

    private async Task ListenOnToggled(bool listenAny) {
        _proxyConfig!.AdminListenAny = listenAny;
        await SaveChanges();
    }

    public void Dispose() {
        _db?.Dispose();
    }
}
