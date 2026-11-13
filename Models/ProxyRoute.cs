namespace ByProxy.Models {
    public class ProxyRoute : IOrderable, IVersioned {
        public Guid Id { get; set; }

        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public bool Disabled { get; set; }

        public int Order { get; set; }

        public RouteResponseType ResponseType { get; set; }

        public Guid? ClusterId { get; set; }
        public int? ClusterConfigRevision { get; set; }
        public ProxyCluster? Cluster { get; set; }

        public StaticResponse? StaticResponse { get; set; }

        public List<string>? Methods { get; set; }

        public List<string>? Hosts { get; set; }

        public List<ProxyRouteMatch> MatchCriteria { get; set; }

        public bool PreserveHostHeader { get; set; }
        public bool PreserveClientHeaders { get; set; } = true;
        public List<string>? AllowedRequestHeaders { get; set; }

        public const string DEFAULT_XFORWARDED_PREFIX = "X-Forwarded-";
        public string? XForwardedPrefix { get; set; }
        public ForwardedTransformAction? XForwardedForAction { get; set; }
        public ForwardedTransformAction? XForwardedProtoAction { get; set; }
        public ForwardedTransformAction? XForwardedHostAction { get; set; }
        public ForwardedTransformAction? XForwardedPrefixAction { get; set; }

        public string? Path { get; set; }
        public bool PathIsPrefix { get; set; } = true;

        public int? HttpPort { get; set; }
        public int? HttpsPort { get; set; }
        public bool SuppressHttpsRedirect { get; set; }

        public long? MaxRequestBodySize { get; set; }

        public List<string>? AllowedResponseHeaders { get; set; }
        public bool PreserveResponseHeaders { get; set; } = true;
        public List<ProxyRequestTransform> RequestTransforms { get; set; }
        public List<ProxyResponseTransform> ResponseTransforms { get; set; }

        public IpRestrictionMode? IpRestrictionMode { get; set; }
        public List<string>? IpRestrictionEntries { get; set; }
        public StaticResponse? IpDenyResponse { get; set; }

        public string GetPathAsYarpMatch() {
            if (string.IsNullOrWhiteSpace(Path)) {
                return "{**catch-all}";
            } else if (PathIsPrefix) {
                return $"/{Path.Trim().Trim('/')}/{{**remainder}}";
            }
            return Path.Trim();
        }

        public string GetPathVariable() {
            if (string.IsNullOrWhiteSpace(Path)) {
                return "catch-all";
            } else if (PathIsPrefix) {
                return "remainder";
            }
            return string.Empty;
        }

        public async Task<List<string>> NormalizeAndReturnValidationErrors(ProxyDb db, int configRevision) {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Name)) errors.Add("ERR_NameEmpty");

            if (IpRestrictionMode != null && IpRestrictionEntries != null) {
                foreach (var entry in IpRestrictionEntries.WithoutBlankAndCommentLines()) {
                    try {
                        RunningIpRange.Parse(entry);
                    } catch (IpRangeParseException ex) {
                        if (!errors.Contains(ex.ErrorKey)) {
                            errors.Add(ex.ErrorKey);
                        }
                    }
                }
            }

            switch (ResponseType.Type) {
                case RouteResponseType.Constants.Cluster:
                    if (ClusterId == null || ClusterId == Guid.Empty) errors.Add("ERR_NoClusterSelected");
                    break;
                case RouteResponseType.Constants.Redirect:
                    if (StaticResponse?.HttpStatusCode is not (301 or 302 or 307 or 308)) errors.Add("ERR_InvalidRedirectStatus");
                    if (
                        StaticResponse?.Headers == null
                        || !StaticResponse.Headers.TryGetValue("Location", out var location)
                        || string.IsNullOrWhiteSpace(location))
                    {
                        errors.Add("ERR_RedirectLocationRequired");
                    }
                    break;
                case RouteResponseType.Constants.Static:
                case RouteResponseType.Constants.Tarpit:
                    if (StaticResponse == null || StaticResponse.HttpStatusCode == 0) errors.Add("ERR_HttpStatusCodeRequired");
                    break;
            }

            if ((HttpPort == 0 || HttpPort == null) && (HttpsPort == 0 || HttpsPort == null)) {
                errors.Add("ERR_NoProtocols");
            } else {
                if (HttpPort == HttpsPort) errors.Add("ERR_SamePortBothProtocols");

                int adminPort = await db.Configurations
                    .Where(_ => _.Revision == configRevision)
                    .Select(_ => _.AdminPort)
                    .FirstAsync();
                if (adminPort == HttpPort || adminPort == HttpsPort) {
                    errors.Add("ERR_RoutePortInUseAdmin");
                }

                if (HttpPort != null) {
                    bool httpConflict = await db.Routes
                        .AsNoTracking()
                        .Where(_ => _.ConfigRevision == configRevision)
                        .AnyAsync(_ => _.HttpsPort == HttpPort);
                    if (httpConflict) {
                        errors.Add("ERR_HttpRoutePortUsedByHttps");
                    }
                }

                if (HttpsPort != null) {
                    bool httpsConflict = await db.Routes
                        .AsNoTracking()
                        .Where(_ => _.ConfigRevision == configRevision)
                        .AnyAsync(_ => _.HttpPort == HttpsPort);
                    if (httpsConflict) {
                        errors.Add("ERR_HttpsRoutePortUsedByHttp");
                    }
                }

                if (MaxRequestBodySize < -1) MaxRequestBodySize = -1;
            }

            if (Methods == null || Methods.Count == 0) {
                Methods = null;
            } else {
                Methods = Methods.Select(_ => _.ToUpperInvariant()).ToList();
            }

            foreach (var criteria in MatchCriteria) {
                switch (criteria) {
                    case ProxyRouteHeaderMatch headerMatch:
                        if (headerMatch.HeaderMatchMode.HasValues) {
                            if (headerMatch.Values == null || headerMatch.Values.Count == 0) {
                                errors.Add("ERR_HeaderMatchCriteriaMissingValues");
                            }
                        } else {
                            headerMatch.Values = null;
                            headerMatch.CaseSensitiveValues = null;
                        }
                        break;
                    case ProxyRouteQueryMatch queryMatch:
                        if (queryMatch.QueryMatchMode.HasValues) {
                            if (queryMatch.Values == null || queryMatch.Values.Count == 0) {
                                errors.Add("ERR_QueryMatchCriteriaMissingValues");
                            }
                        } else {
                            queryMatch.Values = null;
                            queryMatch.CaseSensitiveValues = null;
                        }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(XForwardedPrefix) || XForwardedPrefix == DEFAULT_XFORWARDED_PREFIX) XForwardedPrefix = null;
            if (XForwardedForAction == ForwardedTransformAction.Set) XForwardedForAction = null;
            if (XForwardedProtoAction == ForwardedTransformAction.Set) XForwardedProtoAction = null;
            if (XForwardedHostAction == ForwardedTransformAction.Set) XForwardedHostAction = null;
            if (XForwardedPrefixAction == ForwardedTransformAction.Set) XForwardedPrefixAction = null;

            return errors;
        }

        public RouteConfig ToYarp() {
            var routeConfig = new RouteConfig() {
                RouteId = Id.ToString(),
                Order = Order,
                ClusterId = ClusterId?.ToString() ?? ProxyCluster.NullCluster.ClusterId,
                Match = new RouteMatch() {
                    Methods = Methods,
                    Hosts = Hosts,
                    Path = GetPathAsYarpMatch(),
                    Headers = MatchCriteria.ToYarpHeaders(),
                    QueryParameters = MatchCriteria.ToYarpQueryParameters(),
                },
                MaxRequestBodySize = MaxRequestBodySize,
                // BEGIN WORKAROUND
                // YARP 2.3 is missing MaxRequestBodySize comparison for detecting route changes
                // TODO: Remove after next YARP stable release
                Metadata = MaxRequestBodySize.HasValue
                    ? new Dictionary<string, string> { ["MaxRequestBodySize"] = MaxRequestBodySize.Value.ToString() }
                    : null
                // END WORKAROUND
            };

            if (ClusterId != null) { // Don't process transforms on Redirect routes
                routeConfig = routeConfig.WithTransformUseOriginalHostHeader(PreserveHostHeader);

                if (AllowedRequestHeaders != null) {
                    routeConfig = routeConfig.WithTransformRequestHeadersAllowed(AllowedRequestHeaders.ToArray());
                } else {
                    routeConfig = routeConfig.WithTransformCopyRequestHeaders(PreserveClientHeaders);
                }

                routeConfig = routeConfig.WithTransformXForwarded(
                    headerPrefix: XForwardedPrefix ?? DEFAULT_XFORWARDED_PREFIX,
                    xDefault: ForwardedTransformActions.Set,
                    xFor: XForwardedForAction?.EnumValue ?? ForwardedTransformActions.Set,
                    xProto: XForwardedProtoAction?.EnumValue ?? ForwardedTransformActions.Set,
                    xHost: XForwardedHostAction?.EnumValue ?? ForwardedTransformActions.Set,
                    xPrefix: XForwardedPrefixAction?.EnumValue ?? ForwardedTransformActions.Set
                );

                foreach (var transform in RequestTransforms) {
                    switch (transform) {
                        case ProxyRequestPathTransform pathTransform:
                            routeConfig = pathTransform.PathMode.Mode switch {
                                RequestPathTransformMode.Constants.Add => routeConfig.WithTransformPathPrefix(new PathString(pathTransform.PathString)),
                                RequestPathTransformMode.Constants.Remove => routeConfig.WithTransformPathRemovePrefix(new PathString(pathTransform.PathString)),
                                RequestPathTransformMode.Constants.Static => routeConfig = routeConfig.WithTransformPathSet(new PathString(pathTransform.PathString)),
                                RequestPathTransformMode.Constants.Pattern => routeConfig.WithTransformPathRouteValues(new PathString(pathTransform.PathString)),
                                _ => routeConfig
                            };
                            break;
                        case ProxyRequestMethodTransform methodTransform:
                            routeConfig = routeConfig.WithTransformHttpMethodChange(methodTransform.FromHttpMethod, methodTransform.ToHttpMethod);
                            break;
                        case ProxyRequestHeaderTransform headerTransform:
                            routeConfig = headerTransform.HeaderOperation.Operation switch {
                                RequestHeaderTransformOperation.Constants.Set => routeConfig.WithTransformRequestHeader(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, false),
                                RequestHeaderTransformOperation.Constants.Append => routeConfig.WithTransformRequestHeader(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, true),
                                RequestHeaderTransformOperation.Constants.Remove => routeConfig.WithTransformRequestHeaderRemove(headerTransform.HeaderName),
                                RequestHeaderTransformOperation.Constants.RouteSet => routeConfig.WithTransformRequestHeaderRouteValue(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, false),
                                RequestHeaderTransformOperation.Constants.RouteAppend => routeConfig.WithTransformRequestHeaderRouteValue(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, true),
                                _ => routeConfig
                            };
                            break;
                        case ProxyRequestQueryTransform queryTransform:
                            routeConfig = queryTransform.QueryOperation.Operation switch {
                                RequestQueryTransformOperation.Constants.Set => routeConfig.WithTransformQueryValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, false),
                                RequestQueryTransformOperation.Constants.Append => routeConfig.WithTransformQueryValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, true),
                                RequestQueryTransformOperation.Constants.Remove => routeConfig.WithTransformQueryRemoveKey(queryTransform.QueryKey),
                                RequestQueryTransformOperation.Constants.RouteSet => routeConfig.WithTransformQueryRouteValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, false),
                                RequestQueryTransformOperation.Constants.RouteAppend => routeConfig.WithTransformQueryRouteValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, true),
                                _ => routeConfig
                            };
                            break;
                    }
                }

                if (AllowedResponseHeaders != null) {
                    routeConfig = routeConfig.WithTransformResponseHeadersAllowed(AllowedResponseHeaders.ToArray());
                } else {
                    routeConfig = routeConfig.WithTransformCopyResponseHeaders(PreserveResponseHeaders);
                }

                foreach (var transform in ResponseTransforms) {
                    switch (transform) {
                        case ProxyResponseHeaderTransform headerTransform:
                            routeConfig = headerTransform.HeaderOperation.Operation switch {
                                ResponseHeaderTransformOperation.Constants.Set => routeConfig.WithTransformResponseHeader(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, false),
                                ResponseHeaderTransformOperation.Constants.Append => routeConfig.WithTransformResponseHeader(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, true),
                                ResponseHeaderTransformOperation.Constants.Remove => routeConfig.WithTransformResponseHeaderRemove(headerTransform.HeaderName),
                                _ => routeConfig
                            };
                            break;
                    }
                }
            }

            return routeConfig;
        }

        public ProxyRoute Clone(int newRevision) {
            return new ProxyRoute() {
                Id = Id,
                ConfigRevision = newRevision,
                Name = Name,
                Description = Description,
                Disabled = Disabled,
                Order = Order,
                ResponseType = ResponseType,
                ClusterId = ClusterId,
                ClusterConfigRevision = newRevision,
                StaticResponse = StaticResponse?.Clone(),
                Methods = Methods?.ToList(),
                Hosts = Hosts?.ToList(),
                MatchCriteria = MatchCriteria.Select(_ => _.Clone(newRevision)).ToList(),
                PreserveHostHeader = PreserveHostHeader,
                PreserveClientHeaders = PreserveClientHeaders,
                AllowedRequestHeaders = AllowedRequestHeaders?.ToList(),
                XForwardedPrefix = XForwardedPrefix,
                XForwardedForAction = XForwardedForAction,
                XForwardedProtoAction = XForwardedProtoAction,
                XForwardedHostAction = XForwardedHostAction,
                XForwardedPrefixAction = XForwardedPrefixAction,
                Path = Path,
                PathIsPrefix = PathIsPrefix,
                HttpPort = HttpPort,
                HttpsPort = HttpsPort,
                SuppressHttpsRedirect = SuppressHttpsRedirect,
                MaxRequestBodySize = MaxRequestBodySize,
                RequestTransforms = RequestTransforms.Select(_ => _.Clone(newRevision)).ToList(),
                PreserveResponseHeaders = PreserveResponseHeaders,
                AllowedResponseHeaders = AllowedResponseHeaders?.ToList(),
                ResponseTransforms = ResponseTransforms.Select(_ => _.Clone(newRevision)).ToList(),
                IpRestrictionMode = IpRestrictionMode,
                IpRestrictionEntries = IpRestrictionEntries?.ToList(),
                IpDenyResponse = IpDenyResponse?.Clone(),
            };
        }

        public RouteExport ToExport() {
            return new RouteExport {
                Id = Id,
                Name = Name,
                Description = Description,
                Disabled = Disabled,
                Order = Order,
                ResponseType = ResponseType.Type,
                ClusterId = ClusterId,
                StaticResponse = StaticResponse?.ToExport(),
                Methods = Methods?.Order().ToList(),
                Hosts = Hosts?.Order().ToList(),
                MatchCriteria = MatchCriteria.OrderBy(_ => _.Id).Select(_ => _.ToExport()).ToList(),
                PreserveHostHeader = PreserveHostHeader,
                PreserveClientHeaders = PreserveClientHeaders,
                AllowedRequestHeaders = AllowedRequestHeaders?.Order().ToList(),
                XForwardedPrefix = XForwardedPrefix,
                XForwardedForAction = XForwardedForAction?.Action,
                XForwardedProtoAction = XForwardedProtoAction?.Action,
                XForwardedHostAction = XForwardedHostAction?.Action,
                XForwardedPrefixAction = XForwardedPrefixAction?.Action,
                Path = Path,
                PathIsPrefix = PathIsPrefix,
                HttpPort = HttpPort,
                HttpsPort = HttpsPort,
                SuppressHttpsRedirect = SuppressHttpsRedirect,
                MaxRequestBodySize = MaxRequestBodySize,
                RequestTransforms = RequestTransforms.OrderBy(_ => _.Id).Select(_ => _.ToExport()).ToList(),
                PreserveResponseHeaders = PreserveResponseHeaders,
                AllowedResponseHeaders = AllowedResponseHeaders?.Order().ToList(),
                ResponseTransforms = ResponseTransforms.OrderBy(_ => _.Id).Select(_ => _.ToExport()).ToList(),
                IpRestrictionMode = IpRestrictionMode?.Mode,
                IpRestrictionEntries = IpRestrictionEntries?.Order(StringComparer.Ordinal).ToList(),
                IpDenyResponse = IpDenyResponse?.ToExport(),
            };
        }
    }

    public class RouteExport {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Disabled { get; set; }
        public int Order { get; set; }
        public string ResponseType { get; set; }
        public Guid? ClusterId { get; set; }
        public StaticResponseExport? StaticResponse { get; set; }
        public List<string>? Methods { get; set; }
        public List<string>? Hosts { get; set; }
        public List<RouteMatchExport> MatchCriteria { get; set; }
        public bool PreserveHostHeader { get; set; }
        public bool PreserveClientHeaders { get; set; }
        public List<string>? AllowedRequestHeaders { get; set; }
        public string? XForwardedPrefix { get; set; }
        public string? XForwardedForAction { get; set; }
        public string? XForwardedProtoAction { get; set; }
        public string? XForwardedHostAction { get; set; }
        public string? XForwardedPrefixAction { get; set; }
        public string? Path { get; set; }
        public bool PathIsPrefix { get; set; }
        public int? HttpPort { get; set; }
        public int? HttpsPort { get; set; }
        public bool SuppressHttpsRedirect { get; set; }
        public long? MaxRequestBodySize { get; set; }
        public List<RequestTransformExport> RequestTransforms { get; set; }
        public bool PreserveResponseHeaders { get; set; }
        public List<string>? AllowedResponseHeaders { get; set; }
        public List<ResponseTransformExport> ResponseTransforms { get; set; }
        public string? IpRestrictionMode { get; set; }
        public List<string>? IpRestrictionEntries { get; set; }
        public StaticResponseExport? IpDenyResponse { get; set; }

        public ProxyRoute Realize(int revision) {
            return new ProxyRoute {
                Id = Id,
                ConfigRevision = revision,
                Name = Name,
                Description = Description,
                Disabled = Disabled,
                Order = Order,
                ResponseType = RouteResponseType.FromString(ResponseType) ?? throw new NotImplementedException($"Unknown Route Response Type: {ResponseType}"),
                ClusterId = ClusterId,
                ClusterConfigRevision = revision,
                StaticResponse = StaticResponse?.Realize(),
                Methods = Methods?.ToList(),
                Hosts = Hosts?.ToList(),
                MatchCriteria = MatchCriteria.Select(_ => _.Realize(revision)).ToList(),
                PreserveHostHeader = PreserveHostHeader,
                PreserveClientHeaders = PreserveClientHeaders,
                AllowedRequestHeaders = AllowedRequestHeaders?.ToList(),
                XForwardedPrefix = XForwardedPrefix,
                XForwardedForAction = XForwardedForAction == null ? null : (ForwardedTransformAction.FromString(XForwardedForAction) ?? throw new NotImplementedException($"Unknown X-Forwarded Action: {XForwardedForAction}")),
                XForwardedProtoAction = XForwardedProtoAction == null ? null : (ForwardedTransformAction.FromString(XForwardedProtoAction) ?? throw new NotImplementedException($"Unknown X-Forwarded Action: {XForwardedProtoAction}")),
                XForwardedHostAction = XForwardedHostAction == null ? null : (ForwardedTransformAction.FromString(XForwardedHostAction) ?? throw new NotImplementedException($"Unknown X-Forwarded Action: {XForwardedHostAction}")),
                XForwardedPrefixAction = XForwardedPrefixAction == null ? null : (ForwardedTransformAction.FromString(XForwardedPrefixAction) ?? throw new NotImplementedException($"Unknown X-Forwarded Action: {XForwardedPrefixAction}")),
                Path = Path,
                PathIsPrefix = PathIsPrefix,
                HttpPort = HttpPort,
                HttpsPort = HttpsPort,
                SuppressHttpsRedirect = SuppressHttpsRedirect,
                MaxRequestBodySize = MaxRequestBodySize,
                RequestTransforms = RequestTransforms.Select(_ => _.Realize(revision)).ToList(),
                PreserveResponseHeaders = PreserveResponseHeaders,
                AllowedResponseHeaders = AllowedResponseHeaders?.ToList(),
                ResponseTransforms = ResponseTransforms.Select(_ => _.Realize(revision)).ToList(),
                IpRestrictionMode = IpRestrictionMode == null ? null : (Models.IpRestrictionMode.FromString(IpRestrictionMode) ?? throw new NotSupportedException($"Unknown IP restriction mode: {IpRestrictionMode}")),
                IpRestrictionEntries = IpRestrictionEntries?.ToList(),
                IpDenyResponse = IpDenyResponse?.Realize(),
            };
        }
    }

    public record RouteResponseType(string Type) {
        public static class Constants {
            public const string Cluster = "Cluster";
            public const string Redirect = "Redirect";
            public const string Static = "Static";
            public const string Tarpit = "Tarpit";
        }

        public static readonly RouteResponseType Cluster = new(Constants.Cluster);
        public static readonly RouteResponseType Redirect = new(Constants.Redirect);
        public static readonly RouteResponseType Static = new(Constants.Static);
        public static readonly RouteResponseType Tarpit = new(Constants.Tarpit);

        public static ImmutableArray<RouteResponseType> RouteResponseTypes = [Cluster, Redirect, Static, Tarpit];

        public static RouteResponseType FromString(string? value) => value switch {
            Constants.Cluster => Cluster,
            Constants.Redirect => Redirect,
            Constants.Static => Static,
            Constants.Tarpit => Tarpit,
            _ => throw new Exception($"Invalid RouteResponseType Type: {value}")
        };

        public class RouteResponseTypeConverter : ValueConverter<RouteResponseType?, string?> {
            public RouteResponseTypeConverter() : base(
                routeResponseType => routeResponseType == null ? null : routeResponseType.Type,
                    value => FromString(value)) { }
        }
    }

    public record IpRestrictionMode(string Mode) {
        public static class Constants {
            public const string AllowList = "AllowList";
            public const string BlockList = "BlockList";
        }

        public static readonly IpRestrictionMode AllowList = new(Constants.AllowList);
        public static readonly IpRestrictionMode BlockList = new(Constants.BlockList);

        public static ImmutableArray<IpRestrictionMode> Modes = [AllowList, BlockList];

        public static IpRestrictionMode? FromString(string? value) => value switch {
            Constants.AllowList => AllowList,
            Constants.BlockList => BlockList,
            _ => null
        };

        public class IpRestrictionModeConverter : ValueConverter<IpRestrictionMode?, string?> {
            public IpRestrictionModeConverter() : base(
                ipRestrictionMode => ipRestrictionMode == null ? null : ipRestrictionMode.Mode,
                value => FromString(value)) { }
        }
    }
}
