namespace ByProxy.AdminApp.Components;
public partial class RouteEditor {
    private bool ListenOnHttp {
        get => Route.HttpPort.HasValue;
        set {
            if (value && !Route.HttpPort.HasValue) Route.HttpPort = 80;
            if (!value) Route.HttpPort = null;
        }
    }

    private bool ListenOnHttps {
        get => Route.HttpsPort.HasValue;
        set {
            if (value && !Route.HttpsPort.HasValue) Route.HttpsPort = 443;
            if (!value) Route.HttpsPort = null;
        }
    }

    private bool RedirectToHttps {
        get => !Route.SuppressHttpsRedirect;
        set { Route.SuppressHttpsRedirect = !value; }
    }

    private string _newAllowedHeader = string.Empty;
    private string _newHost = string.Empty;
    private string _newMethod = string.Empty;

    private string _pathMatchInput = string.Empty;
    private string PathMatch {
        get => Route.Path ?? string.Empty;
        set {
            if (string.IsNullOrWhiteSpace(value)) {
                Route.Path = null;
            } else {
                Route.Path = value.Trim();
            }
        }
    }

    private string _newHeaderMatchKey = string.Empty;
    private ProxyHeaderMatchMode _newHeaderMatchMode = ProxyHeaderMatchMode.Exact;
    private List<string> _newHeaderMatchValues = new();
    private bool _newHeaderMatchCaseSensitivity;

    private string _newQueryMatchKey = string.Empty;
    private ProxyQueryMatchMode _newQueryMatchMode = ProxyQueryMatchMode.Exact;
    private List<string> _newQueryMatchValues = new();
    private bool _newQueryMatchCaseSensitivity;

    private List<ProxyCluster>? _clusters;

    private string _newFromHttpMethod = string.Empty;
    private string _newToHttpMethod = string.Empty;

    private string _newHeaderTransformOperation = HeaderTransformOperation.Set.Operation;
    private string _newHeaderTransformHeaderName = string.Empty;
    private string _newHeaderTransformHeaderValue = string.Empty;

    private string _newPathTransformMode = PathTransformMode.Add.Mode;
    private string _newPathTransformString = string.Empty;

    private string _newQueryTransformOperation = QueryTransformOperation.Set.Operation;
    private string _newQueryTransformQueryKey = string.Empty;
    private string _newQueryTransformQueryValue = string.Empty;

    protected override async Task OnInitializedAsync() {
        using var db = _dbFactory.CreateDbContext();
        _clusters = await db.Clusters
            .AsNoTracking()
            .Include(_ => _.Destinations.OrderBy(d => d.Order))
            .OrderBy(_ => _.Name)
            .ToListAsync();
    }

    private void ChangeRouteResponseType(string? responseType) {
        if (string.IsNullOrEmpty(responseType) || responseType == Route.ResponseType.Type) return;

        Route.ResponseType = RouteResponseType.FromString(responseType);
        switch (responseType) {
            case RouteResponseType.Constants.Cluster:
                Route.ClusterId = Guid.Empty;
                Route.HttpStatusCode = null;
                Route.RedirectLocation = null;
                break;
            case RouteResponseType.Constants.Redirect:
                Route.ClusterId = null;
                Route.HttpStatusCode = 307;
                Route.RedirectLocation = string.Empty;
                break;
            case RouteResponseType.Constants.Status:
                Route.ClusterId = null;
                Route.HttpStatusCode = 404;
                Route.RedirectLocation = null;
                break;
            case RouteResponseType.Constants.Tarpit:
                Route.ClusterId = null;
                Route.HttpStatusCode = 500;
                Route.RedirectLocation = null;
                break;
        }
    }

    private void AddHost() {
        var newHost = _newHost.Trim();
        if (string.IsNullOrWhiteSpace(newHost)) return;
        if (Route.Hosts == null) Route.Hosts = new();
        if (!Route.Hosts.Contains(newHost, StringComparer.OrdinalIgnoreCase)) Route.Hosts.Add(newHost);
        _newHost = string.Empty;
    }

    private void RemoveHost(string host) {
        _newHost = host;
        if (Route.Hosts == null) return;
        Route.Hosts.Remove(host);
        if (Route.Hosts.Count == 0) Route.Hosts = null;
    }

    private async Task AddMethod() {
        var newMethod = _newMethod.Trim();
        if (newMethod != newMethod.ToUpperInvariant()) {
            var result = await _modal.YesNo(_strings["Case Sensitive Method"], _strings["WARN_CaseSensitiveMethod"]);
            if (result != DialogResult.Yes) return;
        }
        if (string.IsNullOrWhiteSpace(newMethod)) return;
        if (Route.Methods == null) Route.Methods = new();
        if (!Route.Methods.Contains(newMethod)) Route.Methods.Add(newMethod);
        _newMethod = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    private void RemoveMethod(string method) {
        _newMethod = method;
        if (Route.Methods == null) return;
        Route.Methods.Remove(method);
        if (Route.Methods.Count == 0) Route.Methods = null;
    }

    private async Task AddNewHeaderMatchValue() {
        var result = await _modal.SingleValue(_strings["New Value"], _strings["Prompt_NewMatchValue"]);
        if (result.DialogResult != DialogResult.OK) return;
        _newHeaderMatchValues.Add(result.Value);
    }

    private void AddHeaderMatch() {
        if (string.IsNullOrWhiteSpace(_newHeaderMatchKey)) return;
        Route.MatchCriteria.Add(new ProxyRouteHeaderMatch {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            Key = _newHeaderMatchKey.Trim(),
            HeaderMatchMode = _newHeaderMatchMode,
            Values = _newHeaderMatchMode.HasValues ? _newHeaderMatchValues : null,
            CaseSensitiveValues = _newHeaderMatchMode.HasValues ? _newHeaderMatchCaseSensitivity : null
        });
        _newHeaderMatchKey = string.Empty;
        _newHeaderMatchMode = ProxyHeaderMatchMode.Exact;
        _newHeaderMatchValues = new();
        _newHeaderMatchCaseSensitivity = false;
    }

    private void RemoveHeaderMatch(ProxyRouteHeaderMatch match) {
        _newHeaderMatchKey = match.Key;
        _newHeaderMatchMode = match.HeaderMatchMode;
        _newHeaderMatchValues = match.Values ?? new();
        _newHeaderMatchCaseSensitivity = match.CaseSensitiveValues ?? false;
        Route.MatchCriteria.Remove(match);
    }

    private async Task AddNewQueryMatchValue() {
        var result = await _modal.SingleValue(_strings["New Value"], _strings["Prompt_NewMatchValue"]);
        if (result.DialogResult != DialogResult.OK) return;
        _newQueryMatchValues.Add(result.Value);
    }

    private void AddQueryMatch() {
        if (string.IsNullOrWhiteSpace(_newQueryMatchKey)) return;
        Route.MatchCriteria.Add(new ProxyRouteQueryMatch {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            Key = _newQueryMatchKey.Trim(),
            QueryMatchMode = _newQueryMatchMode,
            Values = _newQueryMatchMode.HasValues ? _newQueryMatchValues : null,
            CaseSensitiveValues = _newQueryMatchMode.HasValues ? _newQueryMatchCaseSensitivity : null
        });
        _newQueryMatchKey = string.Empty;
        _newQueryMatchMode = ProxyQueryMatchMode.Exact;
        _newQueryMatchValues = new();
        _newQueryMatchCaseSensitivity = false;
    }

    private void RemoveQueryMatch(ProxyRouteQueryMatch match) {
        _newQueryMatchKey = match.Key;
        _newQueryMatchMode = match.QueryMatchMode;
        _newQueryMatchValues = match.Values ?? new();
        _newQueryMatchCaseSensitivity = match.CaseSensitiveValues ?? false;
        Route.MatchCriteria.Remove(match);
    }

    private void SetClientHeaderMode(string? mode) {
        if (mode == "False") {
            Route.PreserveClientHeaders = false;
            Route.AllowedHeaders = null;
        } else if (mode == "Custom") {
            Route.PreserveClientHeaders = true;
            Route.AllowedHeaders = new();
        } else {
            Route.PreserveClientHeaders = true;
            Route.AllowedHeaders = null;
        }
    }

    private void AddAllowedHeader() {
        var newHeader = _newAllowedHeader.Trim();
        if (string.IsNullOrWhiteSpace(newHeader)) return;
        if (Route.AllowedHeaders == null) Route.AllowedHeaders = new();
        if (!Route.AllowedHeaders.Contains(newHeader, StringComparer.OrdinalIgnoreCase)) Route.AllowedHeaders.Add(newHeader);
        _newAllowedHeader = string.Empty;
    }

    private void RemoveAllowedHeader(string header) {
        _newAllowedHeader = header;
        if (Route.AllowedHeaders == null) return;
        Route.AllowedHeaders.Remove(header);
    }

    private void AddPathTransform() {
        PathTransformMode? mode = PathTransformMode.FromString(_newPathTransformMode);
        if (mode == null) return;
        if (string.IsNullOrWhiteSpace(_newPathTransformString)) return;
        if (!_newPathTransformString.StartsWith('/')) _newPathTransformString = $"/{_newPathTransformString}";

        Route.Transforms.Add(new ProxyRoutePathTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            PathMode = mode,
            PathString = _newPathTransformString
        });

        _newPathTransformMode = PathTransformMode.Add.Mode;
        _newPathTransformString = string.Empty;
    }

    private void RemovePathTransform(ProxyRoutePathTransform transform) {
        _newPathTransformMode = transform.PathMode.Mode;
        _newPathTransformString = transform.PathString;
        Route.Transforms.Remove(transform);
    }

    private void AddHeaderTransform() {
        HeaderTransformOperation? operation = HeaderTransformOperation.FromString(_newHeaderTransformOperation);
        if (operation == null) return;
        if (string.IsNullOrWhiteSpace(_newHeaderTransformHeaderName)) return;

        string? headerValue;
        if (operation == HeaderTransformOperation.Remove) {
            headerValue = null;
        } else {
            if (string.IsNullOrWhiteSpace(_newHeaderTransformHeaderValue)) return;
            headerValue = _newHeaderTransformHeaderValue;
        }

        Route.Transforms.Add(new ProxyRouteHeaderTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            HeaderOperation = operation,
            HeaderName = _newHeaderTransformHeaderName,
            HeaderValue = headerValue
        });

        _newHeaderTransformOperation = HeaderTransformOperation.Set.Operation;
        _newHeaderTransformHeaderName = string.Empty;
        _newHeaderTransformHeaderValue = string.Empty;
    }

    private void RemoveHeaderTransform(ProxyRouteHeaderTransform transform) {
        _newHeaderTransformOperation = transform.HeaderOperation.Operation;
        _newHeaderTransformHeaderName = transform.HeaderName;
        _newHeaderTransformHeaderValue = transform.HeaderValue ?? string.Empty;
        Route.Transforms.Remove(transform);
    }

    private void AddMethodTransform() {
        if (string.IsNullOrWhiteSpace(_newFromHttpMethod)) return;
        if (string.IsNullOrWhiteSpace(_newToHttpMethod)) return;
        
        Route.Transforms.Add(new ProxyRouteMethodTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            FromHttpMethod = _newFromHttpMethod,
            ToHttpMethod = _newToHttpMethod
        });

        _newFromHttpMethod = string.Empty;
        _newToHttpMethod = string.Empty;
    }

    private void RemoveMethodTransform(ProxyRouteMethodTransform transform) {
        _newFromHttpMethod = transform.FromHttpMethod;
        _newToHttpMethod = transform.ToHttpMethod;
        Route.Transforms.Remove(transform);
    }

    private void AddQueryTransform() {
        QueryTransformOperation? operation = QueryTransformOperation.FromString(_newQueryTransformOperation);
        if (operation == null) return;
        if (string.IsNullOrWhiteSpace(_newQueryTransformQueryKey)) return;

        string? QueryValue;
        if (operation == QueryTransformOperation.Remove) {
            QueryValue = null;
        } else {
            if (string.IsNullOrWhiteSpace(_newQueryTransformQueryValue)) return;
            QueryValue = _newQueryTransformQueryValue;
        }

        Route.Transforms.Add(new ProxyRouteQueryTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            QueryOperation = operation,
            QueryKey = _newQueryTransformQueryKey,
            QueryValue = QueryValue
        });

        _newQueryTransformOperation = QueryTransformOperation.Set.Operation;
        _newQueryTransformQueryKey = string.Empty;
        _newQueryTransformQueryValue = string.Empty;
    }

    private void RemoveQueryTransform(ProxyRouteQueryTransform transform) {
        _newQueryTransformOperation = transform.QueryOperation.Operation;
        _newQueryTransformQueryKey = transform.QueryKey;
        _newQueryTransformQueryValue = transform.QueryValue ?? string.Empty;
        Route.Transforms.Remove(transform);
    }
}
