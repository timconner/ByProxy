namespace ByProxy.AdminApp.Pages.SystemSettings;
public partial class ConfigComparer {
    private List<ProxyConfig>? _availableConfigs;
    private int _compareRev;
    private int _compareToRev;

    private bool _sortByOrder;
    private string _from = string.Empty;
    private string _to = string.Empty;

    protected override async Task OnInitializedAsync() {
        _compareRev = _proxyState.RunningConfig.Revision;
        _compareToRev = _proxyState.CandidateConfigRevision;

        using var db = _dbFactory.CreateDbContext();
        _availableConfigs = await db.Configurations
            .IgnoreQueryFilters()
            .IgnoreAutoIncludes()
            .OrderByDescending(_ => _.Revision)
            .ToListAsync();

        await UpdateDiff();
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

    private async Task UpdateDiff() {
        await _modal.PerformProcessing(async () => {
            using var db = _dbFactory.CreateDbContext();

            var compareConfig = await db.Configurations.SelectSpecificRevision(_compareRev);
            if (compareConfig == null) throw new Exception("Failed to load current config.");

            var compareToConfig = await db.Configurations.SelectSpecificRevision(_compareToRev);
            if (compareToConfig == null) throw new Exception("Failed to load current config.");

            _from = compareConfig.GenerateComparableJson(_sortByOrder);
            _to = compareToConfig.GenerateComparableJson(_sortByOrder);
        });
    }

    private async Task SortToggled(bool sortByOrder) {
        _sortByOrder = sortByOrder;
        await UpdateDiff();
    }
}
