using System.Threading.Tasks;

namespace ByProxy.AdminApp.Pages.AcmeDns;
public partial class EditDnsProvider {
    [Parameter, EditorRequired]
    public Guid ProviderId { get; set; }
    
    private ElementReference _editorElement;

    private ProxyDb _db = default!;
    private AcmeDnsProvider? _provider;

    private System.Threading.Timer? _monacoWaitTimer;
    private void StopWaitTimer() => _monacoWaitTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    private bool _rendered = false;

    protected override async Task OnInitializedAsync() {
        _db = _dbFactory.CreateDbContext();
        _provider = await _db.AcmeDnsProviders.FirstAsync(_ => _.Id == ProviderId);
        _proxyState.CandidateChanged += CandidateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender) return;
        _rendered = true;
        var monacoLoaded = await _js.InvokeAsync<bool>("isMonacoLoaded");
        if (monacoLoaded && _provider != null) {
            _appState.OnThemeChange += OnThemeChanged;
            await SpawnEditor(null, true);
        } else {
            if (!monacoLoaded) await _js.InvokeVoidAsync("loadMonaco");
            _monacoWaitTimer = new(CheckLoaded, null, 250, 250);
            _modal.StartLoad(_strings["LoadingWait"]);
        }
    }

    private async void CheckLoaded(object? state) {
        if (_provider != null) {
            var monacoLoaded = await _js.InvokeAsync<bool>("isMonacoLoaded");
            if (monacoLoaded) {
                _monacoWaitTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _monacoWaitTimer?.Dispose();
                await _modal.EndLoad(true);
                _appState.OnThemeChange += OnThemeChanged;
                await SpawnEditor(null, true);
            }
        }
    }

    private async void CandidateChanged(object? sender, int revision) {
        if (_provider != null) {
            try {
                var content = await GetEditorContent();
                _provider!.Draft = content;
                await _db.SaveChangesAsync();
            } catch { }
        }
    }

    private async Task SpawnEditor(string? content, bool notifyOnDraft = false) {
        bool showDraftNotice = false;
        if (content == null) {
            if (_provider!.Draft != null) {
                content = _provider.Draft;
                showDraftNotice = notifyOnDraft;
            } else {
                content = _provider.Script ?? AcmeDnsScripting.DNS_PROVIDER_TEMPLATE;
            }
        }

        await _js.InvokeVoidAsync(
            "createMonacoEditor",
            _editorElement,
            "csharp",
            content,
            _appState.PreferredTheme?.IsDarkTheme ?? true
        );

        if (showDraftNotice) {
            await _modal.OkOnly(_strings["Draft Loaded"], _strings["WARN_DraftScriptLoaded"]);
        }
    }

    private async Task<string?> GetEditorContent() {
        try {
            var contentLength = await _js.InvokeAsync<int?>("startMonacoGetContent");
            if (contentLength == null) throw new Exception("Unable to retrieve file contents from editor.");
            int bufferLength = 1024;
            StringBuilder fileContents = new();
            for (int i = 0; i < contentLength; i += bufferLength) {
                fileContents.Append(await _js.InvokeAsync<string>("getMonacoContentBuffer", i, bufferLength));
            }
            await _js.InvokeVoidAsync("endMonacoGetContent");
            return fileContents.ToString();
        } catch (Exception ex) {
            await _modal.OkOnly(_strings["Error"], ex.Message);
        }
        return null;
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
            var content = await GetEditorContent();
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
            var content = await GetEditorContent();
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
        _provider!.Draft = null;
        await _db.SaveChangesAsync();
        await DisposeEditor();
        await SpawnEditor(null);
    }

    private async void OnThemeChanged() => await ReloadEditor();

    private async Task ReloadEditor() {
        var content = await GetEditorContent();
        await DisposeEditor();
        await SpawnEditor(content);
    }

    private async Task DisposeEditor() {
        if (_rendered) {
            try {
                await _js.InvokeVoidAsync("disposeMonacoEditor");
            } catch { }
        }
    }

    public async ValueTask DisposeAsync() {
        _appState.OnThemeChange -= OnThemeChanged;
        _monacoWaitTimer?.Dispose();
        _db?.Dispose();
        await DisposeEditor();
    }
}
