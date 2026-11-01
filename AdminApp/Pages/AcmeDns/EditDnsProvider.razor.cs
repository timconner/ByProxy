namespace ByProxy.AdminApp.Pages.AcmeDns;
public partial class EditDnsProvider {
    [Parameter, EditorRequired]
    public Guid ProviderId { get; set; }

    private MonacoEditor _editor = default!;
    private ProxyDb _db = default!;
    private AcmeDnsProvider? _provider;

    private const string SCRIPT_LANGUAGE = "csharp";

    protected override async Task OnInitializedAsync() {
        _db = _dbFactory.CreateDbContext();
        _provider = await _db.AcmeDnsProviders.FirstAsync(_ => _.Id == ProviderId);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender && _provider != null) {
            _proxyState.CandidateChanged += CandidateChanged;
            if (_provider.Draft != null && _provider.Script != _provider.Draft) {
                await _editor.SetContent(_provider.Draft, SCRIPT_LANGUAGE);
                await _modal.OkOnly(_strings["Draft Loaded"], _strings["WARN_DraftScriptLoaded"]);
            } else {
                await _editor.SetContent(_provider.Script ?? AcmeDnsScripting.DNS_PROVIDER_TEMPLATE, SCRIPT_LANGUAGE);
            }
        }
    }

    private async void CandidateChanged(object? sender, int revision) {
        if (_provider != null) {
            try {
                var content = await _editor.GetContents();
                _provider.Draft = content;
                await _db.SaveChangesAsync();
            } catch { }
        }
    }

    private async Task ShowImports() {
        StringBuilder imports = new();
        foreach (var import in Scripting.AvailableImports) {
            imports.AppendLine($"using {import};");
        }

        await _modal.LargeText(_strings["Imports"], null, imports.ToString(), true, true, true);
    }

    private async Task TestProvider() {
        var domainResult = await _modal.SingleValue(_strings["Test DNS Provider"], _strings["Domain name to test"]);
        if (domainResult.DialogResult != DialogResult.OK || string.IsNullOrWhiteSpace(domainResult.Value)) return;

        IAcmeDnsProvider dnsProvider;
        _modal.StartLoad("Compiling Script");
        try {
            var content = await _editor.GetContents();
            if (content == null) throw new Exception("Unable to retrieve file contents from editor.");
            _provider!.Draft = content;
            await _db.SaveChangesAsync();
            dnsProvider = await AcmeDnsScripting.CompileProviderScript(content);
        } catch (Exception ex) {
            await _modal.EndLoad(false, ex.Message);
            return;
        }
        await _modal.EndLoad(true);

        var txtValue = $"Provider Test: {DateTime.UtcNow.ToString()}";
        var createResult = await _modal.PerformProcessing(async () => {
            var createSuccess = await dnsProvider.CreateDnsRecord(domainResult.Value, txtValue);
            if (!createSuccess) throw new Exception("CreateDnsRecord method returned false.");
        });
        if (!createResult) return;

        var dnsRecord = $"_acme-challenge.{domainResult.Value}.    TXT    \"{txtValue}\"";
        var createConfirm = await _modal.YesNo(_strings["Script Success"], $"{_strings["Prompt_CheckDnsRecordCreated"]}:\n\n{dnsRecord}");
        if (createConfirm != DialogResult.Yes) return;

        var deleteResult = await _modal.PerformProcessing(async () => {
            var deleteSuccess = await dnsProvider.DeleteDnsRecord(domainResult.Value, txtValue);
            if (!deleteSuccess) throw new Exception("DeleteDnsRecord method returned false.");
        });
        if (!deleteResult) return;

        await _modal.OkOnly(_strings["Script Success"], $"{_strings["Prompt_CheckDnsRecordDeleted"]}:\n\n{dnsRecord}");
    }

    private async Task SaveChanges() {
        if (_provider == null) return;
        var success = await _modal.PerformSave(async () => {
            var content = await _editor.GetContents();
            _provider.Script = content;
            _provider.Draft = null;
            await _db.SaveChangesAsync();
            _scriptCompilation.RemoveAcmeDnsProviderFromCache(_provider.Id);
        });
        if (success) _nav.NavigateTo("/acme/dns");
    }

    private async Task DiscardChanges() {
        if (_provider == null) return;
        DialogResult result;
        if (_provider.Draft != null) {
            result = await _modal.YesNo(_strings["Discard Changes"], _strings["Prompt_DeleteDraftAndRevert"]);
        } else {
            result = await _modal.YesNo(_strings["Discard Changes"], _strings["Prompt_DiscardChangesAndReload"]);
        }
        _provider.Draft = null;
        await _db.SaveChangesAsync();
        await _editor.SetContent(_provider.Script ?? AcmeDnsScripting.DNS_PROVIDER_TEMPLATE);
    }

    public async ValueTask DisposeAsync() {
        _proxyState.CandidateChanged -= CandidateChanged;
        _db?.Dispose();
    }
}
