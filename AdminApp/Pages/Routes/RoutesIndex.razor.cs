namespace ByProxy.AdminApp.Pages.Routes;
public partial class RoutesIndex : IDisposable {
    private ProxyDb _db = default!;
    private List<ProxyRoute>? _routes;
    private Dictionary<Guid, string>? _clusterNames;

    private DotNetObjectReference<RoutesIndex>? _dotNetObject;
    private ElementReference _tbody;

    protected override async Task OnInitializedAsync() {
        _db = _dbFactory.CreateDbContext();
        await ReloadRoutes();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender) return;
        _dotNetObject = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("startReorder", _tbody, _dotNetObject);
    }

    private async Task ReloadRoutes() {
        _db.ChangeTracker.Clear();

        _routes = await _db.Routes
            .Ordered()
            .ToListAsync();

        _clusterNames = await _db.Clusters
            .AsNoTracking()
            .ToDictionaryAsync(_ => _.Id, _ => _.Name);
    }

    private List<string> GenerateSimulatedMatches(ProxyRoute route) {
        var matches = new List<string>();
        foreach (var method in route.Methods ?? [string.Empty]) {
            foreach (var host in route.Hosts ?? ["*"]) {
                var path = route.GetPathAsYarpMatch().TrimStart('/');
                path = Regex.Replace(path, @"{\*\*.*}", "*");
                path = path == "*" ? string.Empty : $"/{path}";
                if (route.HttpPort.HasValue) {
                    var port = route.HttpPort == 80 ? string.Empty : $":{route.HttpPort.ToString()}";
                    matches.Add(($"{method} http://{host}{port}{path}").Trim());
                }
                if (route.HttpsPort.HasValue) {
                    var port = route.HttpsPort == 443 ? string.Empty : $":{route.HttpsPort.ToString()}";
                    matches.Add(($"{method} https://{host}{port}{path}").Trim());
                }
            }
        }

        foreach (var criteria in route.MatchCriteria) {
            switch (criteria) {
                case ProxyRouteHeaderMatch headerMatch:
                    switch (headerMatch.HeaderMatchMode.Mode) {
                        case ProxyHeaderMatchMode.Constants.Exact:
                        case ProxyHeaderMatchMode.Constants.Prefix:
                        case ProxyHeaderMatchMode.Constants.Contains:
                            matches.Add($"{_strings["Header"]} '{headerMatch.Key}' {_strings["MatchSummary_Matches"]} ...");
                            break;
                        case ProxyHeaderMatchMode.Constants.NotContains:
                            matches.Add($"{_strings["Header"]} '{headerMatch.Key}' {_strings["MatchSummary_NotMatch"]} ...");
                            break;
                        case ProxyHeaderMatchMode.Constants.Exists:
                            matches.Add($"{_strings["Header"]} '{headerMatch.Key}' {_strings["MatchSummary_Exists"]}");
                            break;
                        case ProxyHeaderMatchMode.Constants.NotExists:
                            matches.Add($"{_strings["Header"]} '{headerMatch.Key}' {_strings["MatchSummary_NotExists"]}");
                            break;
                    }
                    break;
                case ProxyRouteQueryMatch queryMatch:
                    switch (queryMatch.QueryMatchMode.Mode) {
                        case ProxyQueryMatchMode.Constants.Exact:
                        case ProxyQueryMatchMode.Constants.Prefix:
                        case ProxyQueryMatchMode.Constants.Contains:
                            matches.Add($"{_strings["Query Parameter"]} '{queryMatch.Key}' {_strings["MatchSummary_Matches"]} ...");
                            break;
                        case ProxyQueryMatchMode.Constants.NotContains:
                            matches.Add($"{_strings["Query Parameter"]} '{queryMatch.Key}' {_strings["MatchSummary_NotMatch"]} ...");
                            break;
                        case ProxyQueryMatchMode.Constants.Exists:
                            matches.Add($"{_strings["Query Parameter"]} '{queryMatch.Key}' {_strings["MatchSummary_Exists"]}");
                            break;
                    }
                    break;
            }
        }
        return matches;
    }

    [JSInvokable]
    public async void SortableJsUpdate(int oldIndex, int newIndex) {
        if (_routes == null || oldIndex >= _routes.Count || newIndex > _routes.Count) return;
        await _modal.PerformSave(async () => {
            _routes.MoveItemTo(_routes[oldIndex], _routes[newIndex].Order);
            _routes.SortByOrder();
            await _db.SaveChangesAsync();
        });
        _ = _config.UpdateHasChangesPending();
        await InvokeAsync(StateHasChanged);
    }

    private void EditRoute(ProxyRoute route) {
        _nav.NavigateTo($"/routes/{route.Id}");
    }

    private async Task CloneRoute(ProxyRoute route) {
        var result = await _modal.SingleValue(_strings["Clone Route"], _strings["CloneRoutePrompt"], route.Name);
        if (result.DialogResult != DialogResult.OK) return;

        var newRoute = route.Clone(route.ConfigRevision);
        newRoute.Id = Guid.NewGuid();
        newRoute.Name = result.Value;
        foreach (var transform in newRoute.Transforms) {
            transform.Id = Guid.NewGuid();
            transform.RouteId = newRoute.Id;
        }

        var success = await _modal.PerformSave(async () => {
            newRoute.Order = await _db.Routes.MaxAsync(_ => _.Order) + 1;
            _db.Routes.Add(newRoute);
            await _db.SaveChangesAsync();
        });

        if (success) {
            _ = _config.UpdateHasChangesPending();
            await ReloadRoutes();
        }
    }

    private async Task DeleteRoute(ProxyRoute route) {
        var result = await _modal.YesNo(_strings["Delete Route"], $"{_strings["Delete"]}: {route.Name}\n\n{_strings["WARN_Delete"]}");
        if (result != DialogResult.Yes) return;

        var success = await _modal.PerformProcessing(async () => {
            _db.Routes.Remove(route);
            await _db.SaveChangesAsync();
        });

        if (success) {
            _ = _config.UpdateHasChangesPending();
            await ReloadRoutes();
        }
    }

    public void Dispose() {
        _db?.Dispose();
    }
}
