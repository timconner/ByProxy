namespace ByProxy.AdminApp.Components;
public partial class ClusterEditor {
    private string _newName = string.Empty;
    private string _newDestination = string.Empty;
    private string _newHealthCheck = string.Empty;

    private DotNetObjectReference<ClusterEditor>? _dotNetObject;
    private ElementReference _tbody;

    private readonly LoadBalancingPolicy _defaultLbp = LoadBalancingPolicy.AllPolicies.First();
    private LoadBalancingPolicy CurrentOrDefaultLbp => Cluster.LoadBalancing ?? _defaultLbp;

    private bool IsMultiDestination => Cluster.Destinations.Count > 1;
    private bool IsHttpsPresent => Cluster.Destinations.Any(_ => _.Address.StartsWith("https://") || (_.Health?.StartsWith("https://") ?? false));

    protected override void OnInitialized() {
        Cluster.Destinations.NormalizeOrderProperties();
        Cluster.Destinations.SortByOrder();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender) return;
        _dotNetObject = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("startReorder", _tbody, _dotNetObject);
    }

    private Uri ParseHttpUri(string uriString) {
        if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri)) {
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) throw new Exception(_strings["ERR_SchemeMustBeHTTP"]);
            return uri;
        } else {
            throw new Exception(_strings["ERR_UriParseFailed"]);
        }
    }

    private async Task AddDestination() {
        if (string.IsNullOrWhiteSpace(_newDestination)) return;

        try {
            Uri destinationUri = ParseHttpUri(_newDestination);

            Uri? healthUri = null;
            if (!string.IsNullOrWhiteSpace(_newHealthCheck)) {
                try {
                    healthUri = ParseHttpUri(_newHealthCheck);
                } catch (Exception ex) {
                    await _modal.OkOnly(_strings["ERR_BadHealthCheck"], ex.Message);
                    return;
                }
            }
            if (Cluster.Destinations.Any(_ => _.Address == destinationUri.ToString())) {
                await _modal.OkOnly(_strings["Duplicate Destination"], $"{_strings["ERR_DuplicateDestination"]}\n\n{destinationUri.ToString()}");
                return;
            }
            Cluster.Destinations.AppendOrderable(new ProxyDestination {
                Id = Guid.NewGuid(),
                ConfigRevision = Cluster.ConfigRevision,
                ClusterId = Cluster.Id,
                ClusterConfigRevision = Cluster.ConfigRevision,
                Name = _newName,
                Address = destinationUri.ToString(),
                Health = healthUri?.ToString()
            });
            if (Cluster.Destinations.Count > 1 && Cluster.LoadBalancing == null) Cluster.LoadBalancing = _defaultLbp;
            _newName = string.Empty;
            _newDestination = string.Empty;
            _newHealthCheck = string.Empty;
        } catch (Exception ex) {
            await _modal.OkOnly(_strings["ERR_BadDestination"], ex.Message);
        }
    }

    [JSInvokable]
    public async void SortableJsUpdate(int oldIndex, int newIndex) {
        if (oldIndex >= Cluster.Destinations.Count || newIndex > Cluster.Destinations.Count) return;
        Cluster.Destinations.MoveItemTo(Cluster.Destinations[oldIndex], Cluster.Destinations[newIndex].Order);
        Cluster.Destinations.SortByOrder();
        await InvokeAsync(StateHasChanged);
    }

    private void RemoveDestination(ProxyDestination destination) {
        _newName = destination.Name;
        _newDestination = destination.Address;
        _newHealthCheck = destination.Health ?? string.Empty;
        Cluster.Destinations.RemoveOrderable(destination);
    }
}
