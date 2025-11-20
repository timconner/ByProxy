namespace ByProxy.AdminApp.Pages.CertAuthorities;
public partial class CertAuthoritiesIndex {
    private List<CaInfo>? _certAuthorities = null;
    private bool _showHiddenCAs = false;

    private HashSet<Guid> _showInactiveCerts = new();

    private sealed record CaInfo(
        CACert Metadata,
        X509Certificate2 Certificate,
        List<CertInfo> IssuedCerts
    );

    protected override async Task OnInitializedAsync() {
        await ReloadAuthorities();
    }

    private async Task ReloadAuthorities() {
        var db = _dbFactory.CreateDbContext();
        var caResults = await db.CertificateAuthorities
            .AsNoTracking()
            .WhereIsActive(!_showHiddenCAs)
            .ToListAsync();

        var certs = await _certService.CompileAvailableServerCerts<IssuedCert>(_strings);

        var caInfo = new List<CaInfo>();
        foreach (var ca in caResults) {
            var caCert = _certService.GetCertificate(ca.Id);

            var certInfo = new List<CertInfo>();
            foreach (var result in certs.Where(_ => ((IssuedCert)_.Metadata).IssuingCAId == ca.Id)) {
                var cert = _certService.GetCertificate(result.Metadata.Id);
                var subjectNames = Certificates.GetSubjectAltNames(cert);
                certInfo.Add(result);
            }

            caInfo.Add(new CaInfo(ca, caCert, certInfo));
        }

        _certAuthorities = caInfo;
    }

    private async Task RenameCert(CertInfo cert) {
        var result = await _modal.SingleValue(_strings["Rename"], $"{_strings["New Name"]}:", cert.Metadata.Name);
        if (result.DialogResult != DialogResult.OK || string.IsNullOrWhiteSpace(result.Value)) return;
        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            var entry = db.Attach(cert.Metadata);
            entry.Entity.Name = result.Value;
            await db.SaveChangesAsync();
        });
        if (success) await ReloadAuthorities();
    }

    private async Task ChangeCertVisibility(CertInfo cert) {
        bool success = false;
        if (cert.Metadata.Hidden) {
            success = await _modal.PerformSave(async () => {
                using var db = _dbFactory.CreateDbContext();
                var entry = db.Attach(cert.Metadata);
                entry.Entity.Hidden = false;
                await db.SaveChangesAsync();
            });
        } else {
            DialogResult result;
            if (cert.SniHosts.Count > 0) {
                result = await _modal.YesNo($"{_strings["Deactivate"]} - {_strings["In Use"]}", $"{_strings["WARN_CertInUse"]}\n\n{_strings["Prompt_HideCert"]}");
            } else {
                result = await _modal.YesNo(_strings["Deactivate"], _strings["Prompt_HideCert"]);
            }
            if (result != DialogResult.Yes) return;

            success = await _modal.PerformSave(async () => {
                using var db = _dbFactory.CreateDbContext();
                var entry = db.Attach(cert.Metadata);
                entry.Entity.Hidden = true;
                await db.SaveChangesAsync();
            });
        }

        if (success) await ReloadAuthorities();
    }

    private async Task ViewFingerprints(Guid certId) {
        await _modal.CertificateDetails(certId, true);
    }

    private async void ShowInactiveToggled(bool showInactive) {
        _showHiddenCAs = showInactive;
        _certAuthorities = null;
        _showInactiveCerts.Clear();
        await InvokeAsync(StateHasChanged);
        await ReloadAuthorities();
    }

    private void ToggleCaCerts(CaInfo ca, bool showHidden) {
        if (showHidden) {
            _showInactiveCerts.Add(ca.Metadata.Id);
        } else {
            _showInactiveCerts.Remove(ca.Metadata.Id);
        }
    }

    private async Task HideCa(CaInfo ca) {
        DialogResult result;
        if (ca.IssuedCerts.Any(_ => _.SniHosts.Count > 0)) {
            result = await _modal.YesNo($"{_strings["Deactivate"]} - {_strings["In Use"]}", $"{_strings["WARN_CaCertInUse"]}\n\n{_strings["Prompt_HideCa"]}");
        } else {
            result = await _modal.YesNo(_strings["Deactivate"], _strings["Prompt_HideCa"]);
        }
        if (result != DialogResult.Yes) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var entry = db.Attach(ca.Metadata);
            entry.Entity.Hidden = true;
            await db.SaveChangesAsync();
            await db.IssuedCerts
                .Where(_ => _.IssuingCAId == ca.Metadata.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(cert => cert.Hidden, true));
            await transaction.CommitAsync();
        });

        if (success) await ReloadAuthorities();
    }

    private async Task ReactivateCa(CaInfo ca) {
        var result = await _modal.OkCancel(_strings["Reactivate"], _strings["WARN_ReactivateCa"]);
        if (result != DialogResult.OK) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            var entry = db.Attach(ca.Metadata);
            entry.Entity.Hidden = false;
            await db.SaveChangesAsync();
        });

        if (success) await ReloadAuthorities();
    }

    private async Task CreateCa() {
        var result = await _modal.SingleValue(_strings["Create CA"], _strings["Prompt_NameCA"]);
        if (result.DialogResult != DialogResult.OK || string.IsNullOrWhiteSpace(result.Value)) return;

        var success = await _modal.PerformSave(async () => {
            var cert = Certificates.CreateCertificateAuthority(result.Value);
            using var db = _dbFactory.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var entry = db.CertificateAuthorities.Add(new CACert(result.Value));
            await db.SaveChangesAsync();
            Certificates.ExportCertToDisk(entry.Entity.Id, cert);
            await transaction.CommitAsync();
        }, true);

        if (success) await ReloadAuthorities();
    }
}
