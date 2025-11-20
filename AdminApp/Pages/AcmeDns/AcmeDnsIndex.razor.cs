namespace ByProxy.AdminApp.Pages.AcmeDns;
public partial class AcmeDnsIndex {
    private List<DnsProviderInfo>? _dnsProviders;

    private sealed record DnsProviderInfo(
        Guid Id, string Name, List<string> Hosts
    );

    protected override async Task OnInitializedAsync() {
        await ReloadProviders();
    }

    private async Task ReloadProviders() {
        using var db = _dbFactory.CreateDbContext();
        var providers = await db.AcmeDnsProviders
            .AsNoTracking()
            .Select(_ => new { _.Id, _.Name })
            .ToListAsync();

        var dnsHosts = await db.AcmeHosts
            .AsNoTracking()
            .OfType<AcmeDnsCertHost>()
            .Select(_ => new { _.DnsProviderId, _.Host })
            .ToListAsync();

        List<DnsProviderInfo> dnsProviders = new();
        foreach (var provider in providers) {
            dnsProviders.Add(new DnsProviderInfo(
                provider.Id,
                provider.Name,
                dnsHosts.Where(_ => _.DnsProviderId == provider.Id).Select(_ => _.Host).ToList()
            ));
        }

        _dnsProviders = dnsProviders;
    }

    private async Task CreateProvider() {
        var result = await _modal.SingleValue(_strings["DNS Provider Name"], _strings["Prompt_DnsProviderName"]);
        if (result.DialogResult != DialogResult.OK || string.IsNullOrWhiteSpace(result.Value)) return;

        using var db = _dbFactory.CreateDbContext();
        var entry = db.AcmeDnsProviders.Add(new AcmeDnsProvider { Name = result.Value });
        var success = await _modal.PerformSave(async () => {
            await db.SaveChangesAsync();
        });
        if (success) {
            _nav.NavigateTo($"/acme/dns/edit/{entry.Entity.Id}");
        }
    }

    private void EditProvider(DnsProviderInfo provider) {
        _nav.NavigateTo($"/acme/dns/edit/{provider.Id}");
    }

    private async Task RenameProvider(DnsProviderInfo provider) {
        var result = await _modal.SingleValue(_strings["Rename DNS Provider"], $"{_strings["Prompt_RenameDnsProvider"]}:", provider.Name);
        if (result.DialogResult != DialogResult.OK || string.IsNullOrWhiteSpace(result.Value)) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            var entry = db.AcmeDnsProviders.First(_ => _.Id ==  provider.Id);
            entry.Name = result.Value;
            await db.SaveChangesAsync();
        });
        if (success) await ReloadProviders();
    }

    private async Task DeleteProvider(DnsProviderInfo provider) {
        if (provider.Hosts.Count > 0) {
            await _modal.OkOnly(_strings["Error"], _strings["ERR_CannotDeleteProviderInUse"]);
            return;
        }

        var result = await _modal.YesNo(_strings["Delete DNS Provider"], $"{_strings["Prompt_DeleteDnsProvider"]}:\n\n{provider.Name}");
        if (result != DialogResult.Yes) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            await db.AcmeDnsProviders
                .Where(_ => _.Id == provider.Id)
                .ExecuteDeleteAsync();
        }, true);
        if (success) await ReloadProviders();
    }
}
