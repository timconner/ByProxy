namespace ByProxy.AdminApp.Utility {
    public static class RouteExtenstions {
        public static List<string> SimulateRouteMatches(this ProxyRoute route, IStringLocalizer localizer) {
            var matches = new List<string>();
            foreach (var method in route.Methods ?? [string.Empty]) {
                foreach (var host in route.Hosts ?? ["*"]) {
                    var path = route.GetPathAsYarpMatch().TrimStart('/');
                    path = Regex.Replace(path, @"{\*\*.*}", "*");
                    path = path == "*" ? "/*" : $"/{path}";

                    if (route.HttpPort.HasValue) {
                        var port = route.HttpPort == 80 ? string.Empty : $":{route.HttpPort.ToString()}";
                        var target = $"{host}{port}{path}";
                        if (target == "*/*") target = "*";
                        matches.Add(($"{method} http://{target}").Trim());
                    }
                    if (route.HttpsPort.HasValue) {
                        var port = route.HttpsPort == 443 ? string.Empty : $":{route.HttpsPort.ToString()}";
                        var target = $"{host}{port}{path}";
                        if (target == "*/*") target = "*";
                        matches.Add(($"{method} https://{target}").Trim());
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

        public static List<ProxyRoute> GetHiddenByRoutes(this ProxyRoute route, IReadOnlyList<ProxyRoute> allRoutes) {
            var hiddenBy = new List<ProxyRoute>();
            if (route.Disabled) return hiddenBy;

            foreach (var preceding in allRoutes.Ordered()) {
                if (preceding.Id == route.Id) break;
                if (preceding.Disabled) continue;

                if (
                    preceding.MethodRouteMatchShadows(route)
                    && preceding.SchemeRouteMatchShadows(route)
                    && preceding.HostRouteMatchShadows(route)
                    && preceding.PathRouteMatchShadows(route)
                    && preceding.HeaderRouteMatchShadows(route)
                    && preceding.QueryRouteMatchShadows(route)
                ) {
                    hiddenBy.Add(preceding);
                }
            }
            return hiddenBy;
        }

        private static bool MethodRouteMatchShadows(this ProxyRoute hidingRoute, ProxyRoute targetRoute) {
            if (hidingRoute.Methods == null) return true;
            if (targetRoute.Methods == null) return false;
            if (targetRoute.Methods.Count > hidingRoute.Methods.Count) return false;
            return targetRoute.Methods.ToHashSet().IsSubsetOf(hidingRoute.Methods.ToHashSet());
        }

        private static bool SchemeRouteMatchShadows(this ProxyRoute hidingRoute, ProxyRoute targetRoute) {
            if (targetRoute.HttpPort is int targetHttpPort && hidingRoute.HttpPort != targetHttpPort) return false;
            if (targetRoute.HttpsPort is int targetHttpsPort && hidingRoute.HttpsPort != targetHttpsPort) return false;
            return true;
        }

        private static bool HostRouteMatchShadows(this ProxyRoute hidingRoute, ProxyRoute targetRoute) {
            if (hidingRoute.Hosts == null) return true;
            if (targetRoute.Hosts == null) return false;
            return targetRoute.Hosts.All(target => hidingRoute.Hosts.Any(hider => HostShadows(hider, target)));
        }

        private static bool HostShadows(string hidingHost, string targetHost) {
            if (hidingHost == "*") return true;
            if (targetHost == "*") return false;
            if (hidingHost.StartsWith("*.")) {
                if (targetHost.StartsWith("*.")) {
                    return string.Equals(hidingHost, targetHost, StringComparison.OrdinalIgnoreCase);
                }
                return targetHost.EndsWith(hidingHost.Substring(1), StringComparison.OrdinalIgnoreCase);
            }
            if (targetHost.StartsWith("*.")) return false;
            return string.Equals(hidingHost, targetHost, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathRouteMatchShadows(this ProxyRoute hidingRoute, ProxyRoute targetRoute) {
            if (PathMatchesAll(hidingRoute.Path, hidingRoute.PathIsPrefix)) return true;
            if (PathMatchesAll(targetRoute.Path, targetRoute.PathIsPrefix)) return false;

            // Parameter Matching Detected
            if (hidingRoute.Path.Contains('{') || targetRoute.Path.Contains('{')) return false;

            var hidingSegments = GetSegments(hidingRoute.Path);
            var targetSegments = GetSegments(targetRoute.Path);

            if (hidingRoute.PathIsPrefix) {
                if (targetSegments.Count < hidingSegments.Count) return false;
                return AllSegmentsEqual(hidingSegments, targetSegments);
            }

            if (targetRoute.PathIsPrefix) return false;
            return hidingSegments.Count == targetSegments.Count && AllSegmentsEqual(hidingSegments, targetSegments);
        }

        private static bool PathMatchesAll([NotNullWhen(false)] string? path, bool isPrefix) {
            if (string.IsNullOrWhiteSpace(path)) return true;
            return isPrefix && GetSegments(path).Count == 0;
        }

        private static List<string> GetSegments(string path) {
            return path.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(_ => _.ToLowerInvariant())
                .ToList();
        }

        private static bool AllSegmentsEqual(List<string> hiding, List<string> target) {
            for (var i = 0; i < hiding.Count; i++) {
                if (hiding[i] != target[i]) return false;
            }
            return true;
        }

        private static bool HeaderRouteMatchShadows(this ProxyRoute hidingRoute, ProxyRoute targetRoute) {
            return CriteriaRouteMatchShadows<ProxyRouteHeaderMatch>(hidingRoute, targetRoute, match => match.HeaderMatchMode.Mode);
        }

        private static bool QueryRouteMatchShadows(this ProxyRoute hidingRoute, ProxyRoute targetRoute) {
            return CriteriaRouteMatchShadows<ProxyRouteQueryMatch>(hidingRoute, targetRoute, match => match.QueryMatchMode.Mode);
        }

        private static bool CriteriaRouteMatchShadows<T>(ProxyRoute hidingRoute, ProxyRoute targetRoute, Func<T, string> getMode) where T : ProxyRouteMatch {
            var targetByKey = targetRoute.MatchCriteria
                .OfType<T>()
                .ToLookup(match => match.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var hidingMatch in hidingRoute.MatchCriteria.OfType<T>()) {
                var hidingMode = getMode(hidingMatch);
                var targetMatches = targetByKey[hidingMatch.Key];

                var hidingMatchIsGuaranteed = targetMatches.Any(targetMatch =>
                    CriterionShadows(hidingMatch, targetMatch, hidingMode, getMode(targetMatch)));

                if (!hidingMatchIsGuaranteed) return false;
            }

            return true;
        }

        private static bool CriterionShadows(ProxyRouteMatch hidingMatch, ProxyRouteMatch targetMatch, string hidingMode, string targetMode) {
            if (hidingMode == ProxyRouteMatchModes.Exists) {
                return targetMode is not (ProxyRouteMatchModes.NotExists or ProxyRouteMatchModes.NotContains);
            }

            if (hidingMode == ProxyRouteMatchModes.NotExists) {
                return targetMode == ProxyRouteMatchModes.NotExists;
            }

            // Mode is Exact, Prefix, Contains, or NotContains
            if (hidingMatch.Values == null || hidingMatch.Values.Count == 0) return false;
            if (targetMatch.Values == null || targetMatch.Values.Count == 0) return false;

            if (hidingMode == ProxyRouteMatchModes.NotContains) {
                return NotContainsCriterionShadows(
                    hidingMatch.Values, targetMatch.Values, targetMode,
                    hidingMatch.CaseSensitiveValues ?? false, targetMatch.CaseSensitiveValues ?? false);
            }

            return PositiveCriterionShadows(
                hidingMatch.Values, targetMatch.Values, hidingMode, targetMode,
                hidingMatch.CaseSensitiveValues ?? false, targetMatch.CaseSensitiveValues ?? false);
        }

        private static bool PositiveCriterionShadows(IReadOnlyList<string> hidingValues, IReadOnlyList<string> targetValues, string hidingMode, string targetMode, bool hidingCaseSensitive, bool targetCaseSensitive) {
            if (hidingCaseSensitive && !targetCaseSensitive) return false;

            var comparison = hidingCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            switch (hidingMode) {
                case ProxyRouteMatchModes.Exact:
                    if (targetMode != ProxyRouteMatchModes.Exact) return false;
                    return targetValues.All(targetValue =>
                        hidingValues.Any(hidingValue =>
                            string.Equals(targetValue, hidingValue, comparison)));

                case ProxyRouteMatchModes.Prefix:
                    if (targetMode != ProxyRouteMatchModes.Exact && targetMode != ProxyRouteMatchModes.Prefix) return false;
                    return targetValues.All(targetValue =>
                        hidingValues.Any(hidingPrefix =>
                            targetValue.StartsWith(hidingPrefix, comparison)));

                case ProxyRouteMatchModes.Contains:
                    if (targetMode is ProxyRouteMatchModes.Exact or ProxyRouteMatchModes.Prefix or ProxyRouteMatchModes.Contains) {
                        return targetValues.All(targetValue =>
                            hidingValues.Any(hidingSubstring => targetValue.Contains(hidingSubstring, comparison)));
                    }
                    return false;

                default:
                    return false;
            }
        }

        private static bool NotContainsCriterionShadows(IReadOnlyList<string> hidingValues, IReadOnlyList<string> targetValues, string targetMode, bool hidingCaseSensitive, bool targetCaseSensitive) {
            if (targetMode != ProxyRouteMatchModes.NotContains) return false;
            if (!hidingCaseSensitive && targetCaseSensitive) return false;

            var comparison = targetCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return hidingValues.All(hidingSubstring =>
                targetValues.Any(targetSubstring => hidingSubstring.Contains(targetSubstring, comparison)));
        }
    }
}
