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

    private async Task EnableRoute(ProxyRoute route) {
        var success = await _modal.PerformProcessing(async () => {
            route.Disabled = false;
            await _db.SaveChangesAsync();
        });
        _ = _config.UpdateHasChangesPending();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DisableRoute(ProxyRoute route) {
        var success = await _modal.PerformProcessing(async () => {
            route.Disabled = true;
            await _db.SaveChangesAsync();
        });
        _ = _config.UpdateHasChangesPending();
        await InvokeAsync(StateHasChanged);
    }

    private async Task CloneRoute(ProxyRoute route) {
        var result = await _modal.SingleValue(_strings["Clone Route"], _strings["CloneRoutePrompt"], route.Name);
        if (result.DialogResult != DialogResult.OK) return;

        var newRoute = route.Clone(route.ConfigRevision);
        newRoute.Id = Guid.NewGuid();
        newRoute.Name = result.Value;
        
        foreach (var transform in newRoute.RequestTransforms) {
            transform.Id = Guid.NewGuid();
            transform.RouteId = newRoute.Id;
        }

        foreach (var transform in newRoute.ResponseTransforms) {
            transform.Id = Guid.NewGuid();
            transform.RouteId = newRoute.Id;
        }

        foreach (var matchCriteria in newRoute.MatchCriteria) {
            matchCriteria.Id = Guid.NewGuid();
            matchCriteria.RouteId = newRoute.Id;
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
