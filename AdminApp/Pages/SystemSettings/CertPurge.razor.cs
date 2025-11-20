namespace ByProxy.AdminApp.Pages.SystemSettings;
public partial class CertPurge {
    private List<CertInfo>? _certs;

    private record CaCertAndCount(CACert Metadata, X509Certificate2 Certificate, int IssuedCertCount);
    private List<CaCertAndCount>? _certAuths;

    private record AcmeAccountAndCount(AcmeAccount Account, AcmeProvider Provider, int IssuedCertCount);
    private List<AcmeAccountAndCount>? _acmeAccounts;

    private enum ActiveScreen {
        Certs, CA, ACME
    }
    private ActiveScreen _activeScreen = ActiveScreen.Certs;

    protected override async Task OnInitializedAsync() {
        _certs = await _certService.CompileAvailableServerCerts(_strings, true);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            await _modal.OkOnly(_strings["Certificate Purge Utility"], _strings["Help_CertificatePurgeUtility"]);
        }
    }

    private async Task ChangeScreen() {
        if (_activeScreen == ActiveScreen.CA) {
            if (_certAuths == null) {
                using var db = _dbFactory.CreateDbContext();
                var results = await db.CertificateAuthorities
                    .Select(ca => new { metadata = ca, issueCount = db.IssuedCerts.Count(ic => ic.IssuingCAId == ca.Id) })
                    .ToListAsync();

                var certAuths = new List<CaCertAndCount>();
                foreach (var result in results) {
                    var cert = _certService.GetCertificate(result.metadata.Id);
                    certAuths.Add(new CaCertAndCount(result.metadata, cert, result.issueCount));
                }
                _certAuths = certAuths;
                await InvokeAsync(StateHasChanged);
            }
        } else if (_activeScreen == ActiveScreen.ACME) {
            if (_acmeAccounts == null) {
                using var db = _dbFactory.CreateDbContext();
                var results = await db.AcmeAccounts
                    .Select(account => new { account = account, issueCount = db.AcmeCerts.Count(ac => ac.AcmeAccountId == account.Id) })
                    .ToListAsync();

                var accounts = new List<AcmeAccountAndCount>();
                foreach (var result in results) {
                    accounts.Add(new AcmeAccountAndCount(result.account, _acmeClient.GetProvider(result.account.Provider), result.issueCount));
                }
                _acmeAccounts = accounts;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task CertToPurgeSelected(CertInfo certToPurge) {
        var toPurgeId = certToPurge.Metadata.Id;
        using var db = _dbFactory.CreateDbContext();

        var mapRevisionsUsed = await db.SniMaps
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(_ => _.CertificateId == toPurgeId)
            .Select(_ => _.ConfigRevision)
            .ToListAsync();

        var systemRevisionsUsed = await db.Configurations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(_ => _.AdminCertId == toPurgeId || _.FallbackCertId == toPurgeId)
            .Select(_ => _.Revision)
            .ToListAsync();

        var revisionsUsed = mapRevisionsUsed.Union(systemRevisionsUsed).ToHashSet();

        if (revisionsUsed.Count == 0) {
            var confirmResult = await _modal.YesNo(_strings["Certificate Purge Utility"], $"{_strings["Certificate Name"]}: {certToPurge.Metadata.Name}\n\n{_strings["WARN_ConfirmPurgeNoReplacement"]}");
            if (confirmResult != DialogResult.Yes) return;
            var success = await _modal.PerformSave(async () => {
                await _certService.PurgeCertificate(toPurgeId);
            }, true);
            if (success) _nav.NavigateTo("/system");
        } else {
            bool candidateInUse = revisionsUsed.Contains(_proxyState.CandidateConfigRevision);
            bool runningInUse = revisionsUsed.Contains(_proxyState.RunningConfig.Revision);
            if (candidateInUse || runningInUse) {
                var errors = new List<string>();
                if (candidateInUse) errors.Add(_strings["ERR_PurgeCandidateInUse"]);
                if (runningInUse) errors.Add(_strings["ERR_PurgeRunningInUse"]);
                await _modal.OkOnly(_strings["Error"], string.Join("\n\n", errors));
                return;
            }

            await _modal.OkOnly(_strings["Certificate Purge Utility"], _strings["Help_PurgePriorUse"]);
            var replaceWithResult = await _modal.CertificatePicker(_strings["Select Replacement Certificate"], toPurgeId);
            if (replaceWithResult.DialogResult != DialogResult.OK || replaceWithResult.Value == null || replaceWithResult.Value.Id == toPurgeId) return;

            var confirmResult = await _modal.YesNo(_strings["Certificate Purge Utility"], $"{_strings["Purging"]}: {certToPurge.Metadata.Name}\n{_strings["Replacement"]}: {replaceWithResult.Value.Name}\n\n{_strings["WARN_ConfirmPurgeWithReplacement"]}");
            if (confirmResult != DialogResult.Yes) return;

            var success = await _modal.PerformSave(async () => {
                await _certService.PurgeCertificate(toPurgeId, replaceWithResult.Value.Id);
            }, true);
            if (success) _nav.NavigateTo("/system");
        }
    }

    private async Task PurgeCa(Guid caId) {
        using var db = _dbFactory.CreateDbContext();
        var ca = await db.CertificateAuthorities
            .IgnoreQueryFilters()
            .FirstAsync(_ => _.Id == caId);

        bool hasIssuedCert = await db.IssuedCerts
            .IgnoreQueryFilters()
            .Where(_ => _.IssuingCAId == ca.Id)
            .AnyAsync();

        if (hasIssuedCert) {
            await _modal.OkOnly(_strings["Error"], _strings["INFO_CaPurge"]);
            return;
        }

        var confirmResult = await _modal.YesNo(_strings["Certificate Purge Utility"], $"{_strings["Certificate Name"]}: {ca.Name}\n\n{_strings["WARN_ConfirmPurgeNoReplacement"]}");
        if (confirmResult != DialogResult.Yes) return;

        var success = await _modal.PerformSave(async () => {
            await _certService.PurgeCertificate(ca.Id);
        }, true);
        if (success) _nav.NavigateTo("/system");
    }

    private async Task PurgeAcmeAccount(Guid acmeAccountId) {
        using var db = _dbFactory.CreateDbContext();
        var account = await db.AcmeAccounts
            .IgnoreQueryFilters()
            .FirstAsync(_ => _.Id == acmeAccountId);

        bool hasIssuedCert = await db.AcmeCerts
            .IgnoreQueryFilters()
            .Where(_ => _.AcmeAccountId == account.Id)
            .AnyAsync();

        if (hasIssuedCert) {
            await _modal.OkOnly(_strings["Error"], _strings["INFO_AcmeAccountPurge"]);
            return;
        }

        var confirmResult = await _modal.YesNo(_strings["Certificate Purge Utility"], $"{_strings["Account Name"]}: {account.Name}\n\n{_strings["WARN_ConfirmPurgeNoReplacement"]}");
        if (confirmResult != DialogResult.Yes) return;

        var success = await _modal.PerformSave(async () => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.AcmeAccounts.Remove(account);
            await db.SaveChangesAsync();

            _acmeClient.PurgeAccount(account.Id);

            await transaction.CommitAsync();
        }, true);
        if (success) _nav.NavigateTo("/system");
    }
}
