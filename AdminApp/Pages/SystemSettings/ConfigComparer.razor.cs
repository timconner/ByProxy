namespace ByProxy.AdminApp.Pages.SystemSettings;
public partial class ConfigComparer : IAsyncDisposable {
    private ElementReference _editorElement;
    private bool _sortByOrder;

    private System.Threading.Timer? _monacoWaitTimer;
    private void StopWaitTimer() => _monacoWaitTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    private bool _monacoLoaded = false;
    private bool _rendered = false;

    private List<ProxyConfig>? _availableConfigs;
    private int _compareRev;
    private int _compareToRev;

    protected override async Task OnInitializedAsync() {
        _compareRev = _proxyState.RunningConfig.Revision;
        _compareToRev = _proxyState.CandidateConfigRevision;
        _appState.OnThemeChange += OnThemeChanged;

        using var db = _dbFactory.CreateDbContext();
        _availableConfigs = await db.Configurations
            .IgnoreQueryFilters()
            .IgnoreAutoIncludes()
            .OrderByDescending(_ => _.Revision)
            .ToListAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender) return;
        _rendered = true;
        _monacoLoaded = await _js.InvokeAsync<bool>("isMonacoLoaded");
        if (_monacoLoaded) {
            await SpawnDiff();
        } else {
            await _js.InvokeVoidAsync("loadMonaco");
            _monacoWaitTimer = new(CheckMonacoLoaded, null, 250, 250);
            _modal.StartLoad(_strings["LoadingWait"]);
        }
    }

    private async Task PromoteToCandidate() {
        var result = await _modal.YesNo("RestorePriorRev", $"{_strings["Revision To Restore"]}: {_compareToRev}\n\n{_strings["WARN_RestorePriorRev"]}");
        if (result != DialogResult.Yes) return;

        var success = await _modal.PerformSave(async () => {
            _appState.ConfigurationChangeInProgress = true;
            try {
                await _config.PromoteRevisionToCandidate(_compareToRev);

            } finally {
                _appState.ConfigurationChangeInProgress = false;
            }
        }, true);

        if (success) _nav.NavigateTo("/system", true);
    }

    private async void CheckMonacoLoaded(object? state) {
        _monacoLoaded = await _js.InvokeAsync<bool>("isMonacoLoaded");
        if (_monacoLoaded) {
            _monacoWaitTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _monacoWaitTimer?.Dispose();
            await _modal.EndLoad(true);
            await SpawnDiff();
        }
    }

    private async Task SpawnDiff() {
        await _modal.PerformProcessing(async () => {
            using var db = _dbFactory.CreateDbContext();

            var compareConfig = await db.Configurations.SelectSpecificRevision(_compareRev);
            if (compareConfig == null) throw new Exception("Failed to load current config.");

            var compareToConfig = await db.Configurations.SelectSpecificRevision(_compareToRev);
            if (compareToConfig == null) throw new Exception("Failed to load current config.");

            await InvokeAsync(StateHasChanged);

            await _js.InvokeVoidAsync(
                "createMonacoDiff",
                _editorElement,
                compareConfig.GenerateComparableJson(_sortByOrder),
                compareToConfig.GenerateComparableJson(_sortByOrder),
                _appState.PreferredTheme?.IsDarkTheme ?? true
            );
        });
    }

    private async Task SortToggled(bool sortByOrder) {
        _sortByOrder = sortByOrder;
        await DisposeAsync();
        await SpawnDiff();
    }

    private async void OnThemeChanged() => await ReloadDiff();

    private async Task ReloadDiff() {
        await DisposeDiff();
        await SpawnDiff();
    }

    private async Task DisposeDiff() {
        if (_rendered) {
            try {
                await _js.InvokeVoidAsync("disposeMonacoEditor");
            } catch { }
        }
    }

    public async ValueTask DisposeAsync() {
        _appState.OnThemeChange -= OnThemeChanged;
        _monacoWaitTimer?.Dispose();
        await DisposeDiff();
    }
}
