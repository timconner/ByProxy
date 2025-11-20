namespace ByProxy.AdminApp.Pages.AcmeAccounts;
public partial class CreateAcmeAccount {
    private string _accountName = string.Empty;

    private string _acmeProviderId = string.Empty;
    private AcmeProvider? _acmeProvider;

    private string _newEmail = string.Empty;
    private HashSet<string> _contactEmails = new();

    private void UpdateProvider() {
        _acmeProvider = _acmeClient.GetProvider(_acmeProviderId);
    }

    private void AddContactEmail() {
        var newEmail = _newEmail.Trim();
        if (string.IsNullOrWhiteSpace(newEmail)) return;
        _contactEmails.Add(newEmail);
        _newEmail = string.Empty;
    }

    private void RemoveContactEmail(string email) {
        _contactEmails.Remove(email);
        _newEmail = email;
    }

    private async Task CreateAccount() {
        if (string.IsNullOrWhiteSpace(_accountName)) {
            await _modal.OkOnly(_strings["Error"], _strings["ERR_NameEmpty"]);
            return;
        }

        AcmeProvider provider;
        AcmeDirectory? directory = null;
        try {
            provider = _acmeClient.GetProvider(_acmeProviderId);
            await _modal.PerformProcessing(async () => {
                directory = await _acmeClient.GetDirectoryAsync(provider, _proxyState.StoppingOrRestarting);
            });
        } catch (Exception ex) {
            await _modal.OkOnly(_strings["Error"], ex.Message);
            return;
        }

        if (directory?.Meta != null && !string.IsNullOrWhiteSpace(directory.Meta.TermsOfService)) {
            var accepted = await _modal.YesNo(_strings["ToS"], $"{_strings["Prompt_ToS"]}\n\n{directory.Meta.TermsOfService}");
            if (accepted != DialogResult.Yes) return;
        }

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();

            var accountId = Guid.NewGuid();
            var accountUrl = await _acmeClient.CreateNewAccountAsync(accountId, provider, _contactEmails, _proxyState.StoppingOrRestarting);

            try {
                db.AcmeAccounts.Add(new AcmeAccount {
                    Id = accountId,
                    Name = _accountName,
                    Provider = provider.Id,
                    Url = accountUrl
                });
                await db.SaveChangesAsync();
            } catch {
                _acmeClient.PurgeAccount(accountId);
                throw;
            }
        });

        if (success) {
            _nav.NavigateTo("/acme");
        }
    }
}
