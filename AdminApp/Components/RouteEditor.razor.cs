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

    private string _newStaticResponseHeaderName = string.Empty;
    private string _newStaticResponseHeaderValue = string.Empty;

    private string _newFromHttpMethod = string.Empty;
    private string _newToHttpMethod = string.Empty;

    private string _newRequestHeaderTransformOperation = RequestHeaderTransformOperation.Set.Operation;
    private string _newRequestHeaderTransformHeaderName = string.Empty;
    private string _newRequestHeaderTransformHeaderValue = string.Empty;

    private string _newPathTransformMode = RequestPathTransformMode.Add.Mode;
    private string _newPathTransformString = string.Empty;

    private string _newQueryTransformOperation = RequestQueryTransformOperation.Set.Operation;
    private string _newQueryTransformQueryKey = string.Empty;
    private string _newQueryTransformQueryValue = string.Empty;
        
    private string _newAllowedResponseHeader = string.Empty;

    private string _newResponseHeaderTransformOperation = ResponseHeaderTransformOperation.Set.Operation;
    private string _newResponseHeaderTransformHeaderName = string.Empty;
    private string _newResponseHeaderTransformHeaderValue = string.Empty;

    private static readonly string[] SupportedMethods = [
        "GET", "POST", "PUT", "PATCH", "DELETE",
        "HEAD", "OPTIONS", "TRACE"
    ];

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
            case RouteResponseType.Constants.Static:
                Route.ClusterId = null;
                Route.HttpStatusCode = 200;
                Route.RedirectLocation = null;
                break;
            case RouteResponseType.Constants.Tarpit:
                Route.ClusterId = null;
                Route.HttpStatusCode = 500;
                Route.RedirectLocation = null;
                break;
        }
    }

    private string MaxRequestBodySizeMode {
        get => Route.MaxRequestBodySize switch {
            null => "Inherit",
            -1 => "Unlimited",
            _ => "Limited"
        };
        set {
            Route.MaxRequestBodySize = value switch {
                "Unlimited" => -1L,
                "Limited" => _proxyState.RunningConfig.MaxRequestBodySize ?? 30_000_000L,
                _ => null
            };
        }
    }

    private async Task UpdateMaxRequestBodySize(long? newLimit) {
        Route.MaxRequestBodySize = newLimit == null || newLimit < -1
            ? null
            : newLimit;
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

    private void ToggleMethod(string method, bool enabled) {
        Route.Methods ??= new();
        if (enabled) {
            if (!Route.Methods.Contains(method)) {
                Route.Methods.Add(method);
            }
        } else {
            Route.Methods.Remove(method);
            if (Route.Methods.Count == 0) Route.Methods = null;
        }
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

    private async Task ToggleStaticResponseBody(bool state) {
        if (!state) {
            Route.StaticResponseBody = null;
        } else {
            await EditStaticResponseBody();
        }
    }

    private async Task EditStaticResponseBody() {
        var result = await _modal.LargeText(
            title: _strings["Response Body"],
            prompt: null,
            value: Route.StaticResponseBody ?? string.Empty,
            readOnly: false,
            closeOnly: false,
            monoSpace: true
        );

        if (result.DialogResult == DialogResult.OK) {
            Route.StaticResponseBody = result.Value;
        }
    }

    private async Task AddStaticResponseHeader() {
        var name = _newStaticResponseHeaderName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!Regex.IsMatch(name, @"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$")) {
            await _modal.OkOnly(_strings["Invalid Header"], _strings["WARN_InvalidHeaderName"]);
            return;
        }

        var value = _newStaticResponseHeaderValue;
        if (!Regex.IsMatch(value, @"^[\t\x20-\x7E]*$")) {
            await _modal.OkOnly(_strings["Invalid Header"], _strings["WARN_InvalidHeaderValue"]);
            return;
        }

        if (Route.StaticResponseHeaders != null && Route.StaticResponseHeaders.Keys.Contains(name, StringComparer.OrdinalIgnoreCase)) {
            await _modal.OkOnly(_strings["Duplicate Header"], _strings["WARN_DuplicateHeader"]);
            return;
        }

        if (Route.StaticResponseHeaders == null) Route.StaticResponseHeaders = new();
        Route.StaticResponseHeaders.Add(name, value);

        _newStaticResponseHeaderName = string.Empty;
        _newStaticResponseHeaderValue = string.Empty;
    }

    private void RemoveStaticResponseHeader(string name) {
        if (Route.StaticResponseHeaders == null) return;

        _newStaticResponseHeaderName = name;
        _newStaticResponseHeaderValue = Route.StaticResponseHeaders[name];

        Route.StaticResponseHeaders.Remove(name);
        if (Route.StaticResponseHeaders.Count == 0) Route.StaticResponseHeaders = null;
    }

    private void SetClientHeaderMode(string? mode) {
        if (mode == "False") {
            Route.PreserveClientHeaders = false;
            Route.AllowedRequestHeaders = null;
        } else if (mode == "Custom") {
            Route.PreserveClientHeaders = true;
            Route.AllowedRequestHeaders = new();
        } else {
            Route.PreserveClientHeaders = true;
            Route.AllowedRequestHeaders = null;
        }
    }

    private void AddAllowedHeader() {
        var newHeader = _newAllowedHeader.Trim();
        if (string.IsNullOrWhiteSpace(newHeader)) return;
        if (Route.AllowedRequestHeaders == null) Route.AllowedRequestHeaders = new();
        if (!Route.AllowedRequestHeaders.Contains(newHeader, StringComparer.OrdinalIgnoreCase)) Route.AllowedRequestHeaders.Add(newHeader);
        _newAllowedHeader = string.Empty;
    }

    private void RemoveAllowedHeader(string header) {
        _newAllowedHeader = header;
        if (Route.AllowedRequestHeaders == null) return;
        Route.AllowedRequestHeaders.Remove(header);
    }

    private void AddPathTransform() {
        RequestPathTransformMode? mode = RequestPathTransformMode.FromString(_newPathTransformMode);
        if (mode == null) return;
        if (string.IsNullOrWhiteSpace(_newPathTransformString)) return;
        if (!_newPathTransformString.StartsWith('/')) _newPathTransformString = $"/{_newPathTransformString}";

        Route.RequestTransforms.Add(new ProxyRequestPathTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            PathMode = mode,
            PathString = _newPathTransformString
        });

        _newPathTransformMode = RequestPathTransformMode.Add.Mode;
        _newPathTransformString = string.Empty;
    }

    private void RemovePathTransform(ProxyRequestPathTransform transform) {
        _newPathTransformMode = transform.PathMode.Mode;
        _newPathTransformString = transform.PathString;
        Route.RequestTransforms.Remove(transform);
    }

    private async Task AddRequestHeaderTransform() {
        var operation = RequestHeaderTransformOperation.FromString(_newRequestHeaderTransformOperation);
        if (operation == null) return;

        var name = _newRequestHeaderTransformHeaderName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!Regex.IsMatch(name, @"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$")) {
            await _modal.OkOnly(_strings["Invalid Header"], _strings["WARN_InvalidHeaderName"]);
            return;
        }

        string? value = null;

        if (operation != RequestHeaderTransformOperation.Remove) {
            value = _newRequestHeaderTransformHeaderValue;

            if (!Regex.IsMatch(value, @"^[\t\x20-\x7E]*$")) {
                await _modal.OkOnly(_strings["Invalid Header"], _strings["WARN_InvalidHeaderValue"]);
                return;
            }
        }

        Route.RequestTransforms.Add(new ProxyRequestHeaderTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            HeaderOperation = operation,
            HeaderName = name,
            HeaderValue = value
        });

        _newRequestHeaderTransformOperation = RequestHeaderTransformOperation.Set.Operation;
        _newRequestHeaderTransformHeaderName = string.Empty;
        _newRequestHeaderTransformHeaderValue = string.Empty;
    }

    private void RemoveRequestHeaderTransform(ProxyRequestHeaderTransform transform) {
        _newRequestHeaderTransformOperation = transform.HeaderOperation.Operation;
        _newRequestHeaderTransformHeaderName = transform.HeaderName;
        _newRequestHeaderTransformHeaderValue = transform.HeaderValue ?? string.Empty;
        Route.RequestTransforms.Remove(transform);
    }

    private async Task AddMethodTransform() {
        if (string.IsNullOrWhiteSpace(_newFromHttpMethod)) return;
        if (string.IsNullOrWhiteSpace(_newToHttpMethod)) return;

        var existingFrom = Route.RequestTransforms
            .OfType<ProxyRequestMethodTransform>()
            .FirstOrDefault(_ => _.FromHttpMethod == _newFromHttpMethod);

        if (existingFrom != null) {
            if (
                await _modal.YesNo(_strings["Duplicate Method"], _strings["Prompt_OverwriteMethodTransform"])
                != DialogResult.Yes
            ) {
                return;
            }
            existingFrom.ToHttpMethod = _newToHttpMethod;
        } else {
            Route.RequestTransforms.Add(new ProxyRequestMethodTransform {
                Id = Guid.NewGuid(),
                ConfigRevision = Route.ConfigRevision,
                RouteId = Route.Id,
                RouteConfigRevision = Route.ConfigRevision,
                FromHttpMethod = _newFromHttpMethod,
                ToHttpMethod = _newToHttpMethod
            });
        }

        _newFromHttpMethod = string.Empty;
        _newToHttpMethod = string.Empty;
    }

    private void RemoveMethodTransform(ProxyRequestMethodTransform transform) {
        _newFromHttpMethod = transform.FromHttpMethod;
        _newToHttpMethod = transform.ToHttpMethod;
        Route.RequestTransforms.Remove(transform);
    }

    private void AddQueryTransform() {
        RequestQueryTransformOperation? operation = RequestQueryTransformOperation.FromString(_newQueryTransformOperation);
        if (operation == null) return;
        if (string.IsNullOrWhiteSpace(_newQueryTransformQueryKey)) return;

        string? QueryValue;
        if (operation == RequestQueryTransformOperation.Remove) {
            QueryValue = null;
        } else {
            if (string.IsNullOrWhiteSpace(_newQueryTransformQueryValue)) return;
            QueryValue = _newQueryTransformQueryValue;
        }

        Route.RequestTransforms.Add(new ProxyRequestQueryTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            QueryOperation = operation,
            QueryKey = _newQueryTransformQueryKey,
            QueryValue = QueryValue
        });

        _newQueryTransformOperation = RequestQueryTransformOperation.Set.Operation;
        _newQueryTransformQueryKey = string.Empty;
        _newQueryTransformQueryValue = string.Empty;
    }

    private void RemoveQueryTransform(ProxyRequestQueryTransform transform) {
        _newQueryTransformOperation = transform.QueryOperation.Operation;
        _newQueryTransformQueryKey = transform.QueryKey;
        _newQueryTransformQueryValue = transform.QueryValue ?? string.Empty;
        Route.RequestTransforms.Remove(transform);
    }

    private void SetResponseHeaderMode(string? mode) {
        if (mode == "False") {
            Route.PreserveResponseHeaders = false;
            Route.AllowedResponseHeaders = null;
        } else if (mode == "Custom") {
            Route.PreserveResponseHeaders = true;
            Route.AllowedResponseHeaders = new();
        } else {
            Route.PreserveResponseHeaders = true;
            Route.AllowedResponseHeaders = null;
        }
    }

    private async Task AddAllowedResponseHeader() {
        var name = _newAllowedResponseHeader.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!Regex.IsMatch(name, @"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$")) {
            await _modal.OkOnly(_strings["Invalid Header"], _strings["Invalid response header name."]);
            return;
        }

        if (Route.AllowedResponseHeaders == null) Route.AllowedResponseHeaders = new();

        if (!Route.AllowedResponseHeaders.Contains(name, StringComparer.OrdinalIgnoreCase)) {
            Route.AllowedResponseHeaders.Add(name);
        }

        _newAllowedResponseHeader = string.Empty;
    }

    private void RemoveAllowedResponseHeader(string header) {
        _newAllowedResponseHeader = header;
        Route.AllowedResponseHeaders?.Remove(header);
    }

    private async Task AddResponseHeaderTransform() {
        var operation = ResponseHeaderTransformOperation.FromString(_newResponseHeaderTransformOperation);
        if (operation == null) return;

        var name = _newResponseHeaderTransformHeaderName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!Regex.IsMatch(name, @"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$")) {
            await _modal.OkOnly(_strings["Invalid Header"], _strings["WARN_InvalidHeaderName"]);
            return;
        }

        string? value = null;

        if (operation != ResponseHeaderTransformOperation.Remove) {
            value = _newResponseHeaderTransformHeaderValue;

            if (!Regex.IsMatch(value, @"^[\t\x20-\x7E]*$")) {
                await _modal.OkOnly(_strings["Invalid Header"], _strings["WARN_InvalidHeaderValue"]);
                return;
            }
        }

        Route.ResponseTransforms.Add(new ProxyResponseHeaderTransform {
            Id = Guid.NewGuid(),
            ConfigRevision = Route.ConfigRevision,
            RouteId = Route.Id,
            RouteConfigRevision = Route.ConfigRevision,
            HeaderOperation = operation,
            HeaderName = name,
            HeaderValue = value
        });

        _newResponseHeaderTransformOperation = ResponseHeaderTransformOperation.Set.Operation;
        _newResponseHeaderTransformHeaderName = string.Empty;
        _newResponseHeaderTransformHeaderValue = string.Empty;
    }

    private void RemoveResponseHeaderTransform(ProxyResponseHeaderTransform transform) {
        _newResponseHeaderTransformOperation = transform.HeaderOperation.Operation;
        _newResponseHeaderTransformHeaderName = transform.HeaderName;
        _newResponseHeaderTransformHeaderValue = transform.HeaderValue ?? string.Empty;

        Route.ResponseTransforms.Remove(transform);
    }
}
