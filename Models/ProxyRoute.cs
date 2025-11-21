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

        public int? HttpStatusCode { get; set; }
        public string? RedirectLocation { get; set; }

        public List<string>? Methods { get; set; }

        public List<string>? Hosts { get; set; }

        public List<ProxyRouteMatch> MatchCriteria { get; set; }

        public bool PreserveHostHeader { get; set; }
        public bool PreserveClientHeaders { get; set; } = true;
        public List<string>? AllowedHeaders { get; set; }

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

        public List<ProxyRouteTransform> Transforms { get; set; }

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

            switch (ResponseType.Type) {
                case RouteResponseType.Constants.Cluster:
                    if (ClusterId == null || ClusterId == Guid.Empty) errors.Add("ERR_NoClusterSelected");
                    break;
                case RouteResponseType.Constants.Redirect:
                    if (HttpStatusCode != null || RedirectLocation != null) {
                        if (HttpStatusCode is not (301 or 302 or 307 or 308)) errors.Add("ERR_InvalidRedirectStatus");
                        if (string.IsNullOrWhiteSpace(RedirectLocation)) errors.Add("ERR_RedirectLocationRequired");
                    }
                    break;
                case RouteResponseType.Constants.Status:
                case RouteResponseType.Constants.Tarpit:
                    if (HttpStatusCode == null || HttpStatusCode == 0) errors.Add("ERR_HttpStatusCodeRequired");
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
                }
            };

            if (ClusterId != null) { // Don't process transforms on Redirect routes
                routeConfig = routeConfig.WithTransformUseOriginalHostHeader(PreserveHostHeader);

                if (AllowedHeaders != null) {
                    routeConfig = routeConfig.WithTransformRequestHeadersAllowed(AllowedHeaders.ToArray());
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

                foreach (var transform in Transforms) {
                    switch (transform) {
                        case ProxyRoutePathTransform pathTransform:
                            routeConfig = pathTransform.PathMode.Mode switch {
                                PathTransformMode.Constants.Add => routeConfig.WithTransformPathPrefix(new PathString(pathTransform.PathString)),
                                PathTransformMode.Constants.Remove => routeConfig.WithTransformPathRemovePrefix(new PathString(pathTransform.PathString)),
                                PathTransformMode.Constants.Static => routeConfig = routeConfig.WithTransformPathSet(new PathString(pathTransform.PathString)),
                                PathTransformMode.Constants.Pattern => routeConfig.WithTransformPathRouteValues(new PathString(pathTransform.PathString)),
                                _ => routeConfig
                            };
                            break;
                        case ProxyRouteMethodTransform methodTransform:
                            routeConfig = routeConfig.WithTransformHttpMethodChange(methodTransform.FromHttpMethod, methodTransform.ToHttpMethod);
                            break;
                        case ProxyRouteHeaderTransform headerTransform:
                            routeConfig = headerTransform.HeaderOperation.Operation switch {
                                HeaderTransformOperation.Constants.Set => routeConfig.WithTransformRequestHeader(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, false),
                                HeaderTransformOperation.Constants.Append => routeConfig.WithTransformRequestHeader(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, true),
                                HeaderTransformOperation.Constants.Remove => routeConfig.WithTransformRequestHeaderRemove(headerTransform.HeaderName),
                                HeaderTransformOperation.Constants.RouteSet => routeConfig.WithTransformRequestHeaderRouteValue(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, false),
                                HeaderTransformOperation.Constants.RouteAppend => routeConfig.WithTransformRequestHeaderRouteValue(headerTransform.HeaderName, headerTransform.HeaderValue ?? string.Empty, true),
                                _ => routeConfig
                            };
                            break;
                        case ProxyRouteQueryTransform queryTransform:
                            routeConfig = queryTransform.QueryOperation.Operation switch {
                                QueryTransformOperation.Constants.Set => routeConfig.WithTransformQueryValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, false),
                                QueryTransformOperation.Constants.Append => routeConfig.WithTransformQueryValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, true),
                                QueryTransformOperation.Constants.Remove => routeConfig.WithTransformQueryRemoveKey(queryTransform.QueryKey),
                                QueryTransformOperation.Constants.RouteSet => routeConfig.WithTransformQueryRouteValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, false),
                                QueryTransformOperation.Constants.RouteAppend => routeConfig.WithTransformQueryRouteValue(queryTransform.QueryKey, queryTransform.QueryValue ?? string.Empty, true),
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
                RedirectLocation = RedirectLocation,
                HttpStatusCode = HttpStatusCode,
                Methods = Methods?.ToList(),
                Hosts = Hosts?.ToList(),
                MatchCriteria = MatchCriteria.Select(_ => _.Clone(newRevision)).ToList(),
                PreserveHostHeader = PreserveHostHeader,
                PreserveClientHeaders = PreserveClientHeaders,
                AllowedHeaders = AllowedHeaders?.ToList(),
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
                Transforms = Transforms.Select(_ => _.Clone(newRevision)).ToList()
            };
        }

        public dynamic ToComparable() {
            return new {
                Id = Id,
                Name = Name,
                Description = Description,
                Disabled = Disabled,
                Order = Order,
                ResponseType = ResponseType.Type,
                ClusterId = ClusterId,
                HttpStatusCode = HttpStatusCode,
                RedirectLocation = RedirectLocation,
                Methods = Methods?.OrderBy(m => m).ToArray(),
                Hosts = Hosts?.OrderBy(h => h).ToArray(),
                MatchCriteria = MatchCriteria.OrderBy(_ => _.Id).Select(_ => _.ToComparable()).ToArray(),
                PreserveHostHeader = PreserveHostHeader,
                PreserveClientHeaders = PreserveClientHeaders,
                AllowedHeaders = AllowedHeaders?.OrderBy(h => h).ToArray(),
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
                Transforms = Transforms.OrderBy(_ => _.Id).Select(_ => _.ToComparable()).ToArray(),
            };
        }
    }

    public record RouteResponseType(string Type, string Display) {
        public static class Constants {
            public const string Cluster = "Cluster";
            public const string Redirect = "Redirect";
            public const string Status = "Status";
            public const string Tarpit = "Tarpit";
        }

        public static readonly RouteResponseType Cluster = new(Constants.Cluster, "Forward to Cluster");
        public static readonly RouteResponseType Redirect = new(Constants.Redirect, "HTTP Redirect");
        public static readonly RouteResponseType Status = new(Constants.Status, "Return HTTP Status");
        public static readonly RouteResponseType Tarpit = new(Constants.Tarpit, "Tarpit");

        public static ImmutableArray<RouteResponseType> RouteResponseTypes = [Cluster, Redirect, Status, Tarpit];

        public static RouteResponseType FromString(string? value) => value switch {
            Constants.Cluster => Cluster,
            Constants.Redirect => Redirect,
            Constants.Status => Status,
            Constants.Tarpit => Tarpit,
            _ => throw new Exception($"Invalid RouteResponseType Type: {value}")
        };

        public class RouteResponseTypeConverter : ValueConverter<RouteResponseType?, string?> {
            public RouteResponseTypeConverter() : base(
                routeResponseType => routeResponseType == null ? null : routeResponseType.Type,
                    value => FromString(value)) { }
        }
    }
}
