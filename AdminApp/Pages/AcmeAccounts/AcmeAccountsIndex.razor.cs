namespace ByProxy.AdminApp.Pages.AcmeAccounts;
public partial class AcmeAccountsIndex {
    private List<AccountInfo>? _acmeAccounts;
    private bool _showHiddenAccounts = false;

    private HashSet<Guid> _showInactiveCerts = new();

    private sealed record AccountInfo(
        AcmeAccount Account,
        AcmeProvider Provider,
        List<CertInfo> IssuedCerts
    );

    protected override async Task OnInitializedAsync() {
        await ReloadAccounts();
    }

    private async Task ReloadAccounts() {
        var db = _dbFactory.CreateDbContext();
        var accounts = await db.AcmeAccounts
            .AsNoTracking()
            .Where(_ => _.Hidden == _showHiddenAccounts)
            .ToListAsync();

        var certs = await _certService.CompileAvailableServerCerts<AcmeCert>(_strings);

        var accountInfo = new List<AccountInfo>();
        foreach (var account in accounts) {
            var provider = _acmeClient.GetProvider(account.Provider);
            var certInfo = new List<CertInfo>();
            foreach (var result in certs.Where(_ => ((AcmeCert)_.Metadata).AcmeAccountId == account.Id)) {
                var cert = _certService.GetCertificate(result.Metadata.Id);
                var subjectNames = Certificates.GetSubjectAltNames(cert);
                certInfo.Add(result);
            }
            accountInfo.Add(new AccountInfo(account, provider, certInfo));
        }
        _acmeAccounts = accountInfo;
    }

    private async Task PostAsGetTest(AccountInfo accountInfo) {
        var urlResult = await _modal.SingleValue("POST-as-GET Tester", "POST-as-GET Url:");
        if (urlResult.DialogResult != DialogResult.OK) return;

        string output = string.Empty; ;
        await _modal.PerformProcessing(async () => {
            output = await _acmeClient.TestPostAsGetAsync(accountInfo.Account, urlResult.Value);
        });
        await _modal.LargeText("POST-as-GET Results", null, output, true, true);
    }

    private async void ShowInactiveToggled(bool showInactive) {
        _showHiddenAccounts = showInactive;
        _acmeAccounts = null;
        _showInactiveCerts.Clear();
        await InvokeAsync(StateHasChanged);
        await ReloadAccounts();
    }

    private void ToggleAccountCerts(AccountInfo accountInfo, bool showHidden) {
        if (showHidden) {
            _showInactiveCerts.Add(accountInfo.Account.Id);
        } else {
            _showInactiveCerts.Remove(accountInfo.Account.Id);
        }
    }

    private void EditCert(CertInfo cert) {
        _nav.NavigateTo($"/acme/edit-cert/{cert.Metadata.Id}");
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

        if (success) await ReloadAccounts();
    }

    private async Task HideAccount(AccountInfo accountInfo) {
        DialogResult result;
        if (accountInfo.IssuedCerts.Any(_ => _.SniHosts.Count > 0)) {
            result = await _modal.YesNo($"{_strings["Deactivate"]} - {_strings["In Use"]}", $"{_strings["WARN_AcmeAccountCertInUse"]}\n\n{_strings["Prompt_HideAcmeAccount"]}");
        } else {
            result = await _modal.YesNo(_strings["Deactivate"], _strings["Prompt_HideAcmeAccount"]);
        }
        if (result != DialogResult.Yes) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var entry = db.Attach(accountInfo.Account);
            entry.Entity.Hidden = true;
            await db.SaveChangesAsync();
            await db.AcmeCerts
                .Where(_ => _.AcmeAccountId == accountInfo.Account.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(cert => cert.Hidden, true));
            await transaction.CommitAsync();
        });

        if (success) await ReloadAccounts();
    }

    private async Task ReactivateAccount(AccountInfo accountInfo) {
        var result = await _modal.OkCancel(_strings["Reactivate"], _strings["WARN_ReactivateAcmeAccount"]);
        if (result != DialogResult.OK) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            var entry = db.Attach(accountInfo.Account);
            entry.Entity.Hidden = false;
            await db.SaveChangesAsync();
        });

        if (success) await ReloadAccounts();
    }
}
