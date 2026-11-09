namespace ByProxy.AdminApp.Utility {
    public static class Extenstions {
        public static List<string> SimulateRouteMatches(this ProxyRoute route, IStringLocalizer localizer) {
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
                                matches.Add($"{localizer["Header"]} '{headerMatch.Key}' {localizer["MatchSummary_Matches"]} ...");
                                break;
                            case ProxyHeaderMatchMode.Constants.NotContains:
                                matches.Add($"{localizer["Header"]} '{headerMatch.Key}' {localizer["MatchSummary_NotMatch"]} ...");
                                break;
                            case ProxyHeaderMatchMode.Constants.Exists:
                                matches.Add($"{localizer["Header"]} '{headerMatch.Key}' {localizer["MatchSummary_Exists"]}");
                                break;
                            case ProxyHeaderMatchMode.Constants.NotExists:
                                matches.Add($"{localizer["Header"]} '{headerMatch.Key}' {localizer["MatchSummary_NotExists"]}");
                                break;
                        }
                        break;
                    case ProxyRouteQueryMatch queryMatch:
                        switch (queryMatch.QueryMatchMode.Mode) {
                            case ProxyQueryMatchMode.Constants.Exact:
                            case ProxyQueryMatchMode.Constants.Prefix:
                            case ProxyQueryMatchMode.Constants.Contains:
                                matches.Add($"{localizer["Query Parameter"]} '{queryMatch.Key}' {localizer["MatchSummary_Matches"]} ...");
                                break;
                            case ProxyQueryMatchMode.Constants.NotContains:
                                matches.Add($"{localizer["Query Parameter"]} '{queryMatch.Key}' {localizer["MatchSummary_NotMatch"]} ...");
                                break;
                            case ProxyQueryMatchMode.Constants.Exists:
                                matches.Add($"{localizer["Query Parameter"]} '{queryMatch.Key}' {localizer["MatchSummary_Exists"]}");
                                break;
                        }
                        break;
                }
            }
            return matches;
        }

        public static List<string> SimulateRouteResponse(this ProxyRoute route, IStringLocalizer localizer, string? clusterName) {
            var responseDetails = new List<string>();
            switch (route.ResponseType.Type) {
                case RouteResponseType.Constants.Cluster:
                    responseDetails.Add($"{localizer["RouteSummary_Cluster"]}: {clusterName ?? "Not Selected"}");
                    if (route.MaxRequestBodySize != null) {
                        if (route.MaxRequestBodySize < 0) {
                            responseDetails.Add($"{localizer["Request Body Size Limit"]}: {localizer["Unlimited"]}");
                        } else {
                            responseDetails.Add($"{localizer["Request Body Size Limit"]}: {Functions.GetHumanReadableBytes(route.MaxRequestBodySize.Value)}");
                        }
                    }
                    break;
                case RouteResponseType.Constants.Redirect:
                    responseDetails.Add($"{localizer["RouteSummary_Redirect"]}: {route.RedirectLocation}");
                    break;
                case RouteResponseType.Constants.Static:
                case RouteResponseType.Constants.Tarpit:
                    if (route.ResponseType == RouteResponseType.Tarpit) {
                        responseDetails.Add($"{localizer["RouteSummary_Tarpit"]}: {route.HttpStatusCode}");
                    } else {
                        responseDetails.Add($"{localizer["RouteSummary_Static"]}: {route.HttpStatusCode}");
                    }
                    if (route.StaticResponseBody != null) {
                        responseDetails.Add($"{localizer["With Body"]}: {Functions.GetHumanReadableBytes(Encoding.UTF8.GetByteCount(route.StaticResponseBody))}");
                    }
                    if (route.StaticResponseHeaders != null) {
                        if (route.StaticResponseHeaders.Count == 1) {
                            responseDetails.Add($"{localizer["With Header"]}: {route.StaticResponseHeaders.Keys.First()}");
                        } else {
                            responseDetails.Add(localizer["With Multiple Headers"]);
                        }
                    }
                    break;
            }
            return responseDetails;
        }

        public static List<string> SimulateRequestTransforms(this ProxyRoute route, IStringLocalizer localizer) {
            var details = new List<string>();

            if (route.PreserveHostHeader) {
                details.Add($"{localizer["Host Header"]}: {localizer["PreserveHost_True"]}");
            }

            if (route.AllowedRequestHeaders != null) {
                var headers = route.AllowedRequestHeaders.Count == 0
                    ? localizer["None"].Value
                    : string.Join(", ", route.AllowedRequestHeaders);
                details.Add($"{localizer["Incoming Headers"]}: {localizer["CopyHeaders_Custom"]}: {headers}");
            } else if (!route.PreserveClientHeaders) {
                details.Add($"{localizer["Incoming Headers"]}: {localizer["CopyHeaders_False"]}");
            }

            var prefixChanged = !string.IsNullOrWhiteSpace(route.XForwardedPrefix) && route.XForwardedPrefix != ProxyRoute.DEFAULT_XFORWARDED_PREFIX;
            var forwardedPrefix = prefixChanged ? route.XForwardedPrefix : ProxyRoute.DEFAULT_XFORWARDED_PREFIX;

            if (prefixChanged) {
                details.Add($"X-Forwarded — {localizer["Change Prefix"]}: {route.XForwardedPrefix}");
            }

            var forwardedActions = new[] {
                (Suffix: "For", Action: route.XForwardedForAction?.Action ?? ForwardedTransformAction.Constants.Set),
                (Suffix: "Proto", Action: route.XForwardedProtoAction?.Action ?? ForwardedTransformAction.Constants.Set),
                (Suffix: "Host", Action: route.XForwardedHostAction?.Action ?? ForwardedTransformAction.Constants.Set),
                (Suffix: "Prefix", Action: route.XForwardedPrefixAction?.Action ?? ForwardedTransformAction.Constants.Set)
            };

            foreach (var forwarded in forwardedActions) {
                if (forwarded.Action != ForwardedTransformAction.Constants.Set) {
                    details.Add($"{forwardedPrefix}{forwarded.Suffix}: {localizer[$"ForwardedTransformAction_{forwarded.Action}"]}");
                }
            }

            var pathCount = route.RequestTransforms.OfType<ProxyRequestPathTransform>().Count();
            var methodCount = route.RequestTransforms.OfType<ProxyRequestMethodTransform>().Count();
            var headerCount = route.RequestTransforms.OfType<ProxyRequestHeaderTransform>().Count();
            var queryCount = route.RequestTransforms.OfType<ProxyRequestQueryTransform>().Count();

            if (pathCount > 0) {
                details.Add($"{localizer["Path"]}: {pathCount}");
            }
            if (methodCount > 0) {
                details.Add($"{localizer["HTTP Method"]}: {methodCount}");
            }
            if (headerCount > 0) {
                details.Add($"{localizer["Headers"]}: {headerCount}");
            }
            if (queryCount > 0) {
                details.Add($"{localizer["Query String"]}: {queryCount}");
            }

            if (details.Count == 0 && route.RequestTransforms.Count == 0) {
                details.Add(localizer["Transforms_Empty"]);
            }

            return details;
        }

        public static List<string> SimulateResponseTransforms(this ProxyRoute route, IStringLocalizer localizer) {
            var details = new List<string>();

            if (route.AllowedResponseHeaders != null) {
                var headers = route.AllowedResponseHeaders.Count == 0
                    ? localizer["None"].Value
                    : string.Join(", ", route.AllowedResponseHeaders);

                details.Add($"{localizer["Upstream Headers"]}: {localizer["CopyHeaders_Custom"]}: {headers}");
            } else if (!route.PreserveResponseHeaders) {
                details.Add($"{localizer["Upstream Headers"]}: {localizer["CopyHeaders_False"]}");
            }

            var headerCount = route.ResponseTransforms.OfType<ProxyResponseHeaderTransform>().Count();

            if (headerCount > 0) {
                details.Add($"{localizer["Headers"]}: {headerCount}");
            }

            if (details.Count == 0 && route.ResponseTransforms.Count == 0) {
                details.Add(localizer["Transforms_Empty"]);
            }

            return details;
        }
    }
}
