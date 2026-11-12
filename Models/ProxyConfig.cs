namespace ByProxy.Models {
    public class ProxyConfig {
        public int Revision { get; set; }
        public int BasedOnRevision { get; set; }
        public bool Committed { get; set; }
        public bool Confirmed { get; set; }
        public bool Reverted { get; set; }
        public string? ReversionReason { get; set; }
        public DateTime? CommittedAt { get; set; }
        public int ConfirmSeconds { get; set; }

        public Guid? FallbackCertId { get; set; }
        public ServerCert? FallbackCert { get; set; }

        public bool AdminListenAny { get; set; }
        public int AdminPort { get; set; }
        public Guid AdminCertId { get; set; }
        public ServerCert AdminCert { get; set; }

        public List<ProxyRoute> Routes { get; set; }
        public List<ProxyCluster> Clusters { get; set; }
        public List<ProxySniMap> SniMaps { get; set; }
        public long? MaxRequestBodySize { get; set; }
        public StaticResponse UnmatchedResponse { get; set; }

        public List<string> TrustedProxies { get; set; }
        public int TrustedProxyChainLimit { get; set; }

        public ProxyConfig() {
            Revision = 1;
            Routes = new List<ProxyRoute>();
            Clusters = new List<ProxyCluster>();
            SniMaps = new List<ProxySniMap>();
        }

        public RunningConfig AsRunningConfig() {
            var warnings = new List<string>();
            var errors = new List<string>();
            var httpPorts = new HashSet<int>();
            var httpsPorts = new HashSet<int>();
            var validRoutes = new List<ProxyRoute>();
            var nonClusterRoutes = new Dictionary<string, NonClusterRoute>();
            foreach (var route in Routes) {
                if (route.Disabled) {
                    warnings.Add($"Route {route.Name} is administratively disabled.");
                    continue;
                }

                var error = false;
                if (route.HttpPort != null) {
                    if (route.HttpPort == AdminPort) {
                        errors.Add($"Route {route.Name} ejected. http port {route.HttpPort.Value} conflicts with the admin port.");
                        error = true;
                    } else {
                        httpPorts.Add(route.HttpPort.Value);
                    }
                }
                if (route.HttpsPort != null) {
                    if (route.HttpsPort == AdminPort) {
                        errors.Add($"Route {route.Name} ejected. https port {route.HttpsPort.Value} conflicts with the admin port.");
                        error = true;
                    } else {
                        httpsPorts.Add(route.HttpsPort.Value);
                    }
                }

                switch (route.ResponseType.Type) {
                    case RouteResponseType.Constants.Redirect:
                    case RouteResponseType.Constants.Static:
                    case RouteResponseType.Constants.Tarpit:
                        if (route.StaticResponse?.HttpStatusCode == null || route.StaticResponse.HttpStatusCode == 0) {
                            errors.Add($"Route {route.Name} ejected. Missing required properties for a redirect, static or tarpit response route.");
                            error = true;
                        } else {
                            nonClusterRoutes.Add(route.Id.ToString(), new StaticResponseRoute {
                                Response = route.StaticResponse.AsRunning(warnings.Add)
                            });
                        }
                        break;
                }
                if (!error) validRoutes.Add(route);
            }

            var portConflicts = httpPorts.Intersect(httpsPorts).ToList();
            foreach (var port in portConflicts) {
                var conflictedRoutes = validRoutes.Where(_ => _.HttpPort == port).ToList();
                foreach (var route in conflictedRoutes) {
                    errors.Add($"Route {route.Name} ejected. http port {port} conflicts with the https port this or another route.");
                    validRoutes.Remove(route);
                }
                httpPorts.Remove(port);
            }

            var routeIpRestrictions = new Dictionary<string, RunningIpRestriction>();
            foreach (var route in validRoutes.ToList()) {
                if (route.IpRestrictionMode == null) continue;

                try {
                    if (
                        route.IpRestrictionMode != IpRestrictionMode.AllowList
                        && route.IpRestrictionMode != IpRestrictionMode.BlockList
                    ) {
                        throw new FormatException($"Unknown IP restriction mode: {route.IpRestrictionMode.Mode}");
                    }

                    StaticResponse denyResponse;
                    if (route.IpDenyResponse == null || route.IpDenyResponse.HttpStatusCode == 0) {
                        warnings.Add($"Route {route.Name} has no valid IP denial response. Using an empty 403 response.");
                        denyResponse = StaticResponse.CreateDefault(403);
                    } else {
                        denyResponse = route.IpDenyResponse;
                    }

                    var ranges = (route.IpRestrictionEntries ?? new List<string>())
                        .Select(RunningIpRange.Parse)
                        .ToImmutableArray();

                    if (ranges.IsEmpty) {
                        warnings.Add(route.IpRestrictionMode == IpRestrictionMode.AllowList
                            ? $"Route {route.Name} has an empty IP allow list. All clients will be denied."
                            : $"Route {route.Name} has an empty IP block list. No clients will be blocked.");
                    }

                    routeIpRestrictions.Add(route.Id.ToString(), new RunningIpRestriction {
                        IsAllowList = route.IpRestrictionMode == IpRestrictionMode.AllowList,
                        Ranges = ranges,
                        DenyResponse = denyResponse.AsRunning(warnings.Add)
                    });
                } catch (FormatException ex) {
                    errors.Add($"Route {route.Name} ejected. {ex.Message}");
                    validRoutes.Remove(route);
                    nonClusterRoutes.Remove(route.Id.ToString());
                }
            }

            var clusters = Clusters.Select(_ => _.ToYarp()).ToList();
            clusters.Add(ProxyCluster.NullCluster);

            var trustedProxies = new List<System.Net.IPNetwork>();
            var trustedProxyChainLimit = TrustedProxyChainLimit;
            if (trustedProxyChainLimit < 0) {
                warnings.Add("Trusted proxy chain limit cannot be negative. Forwarded client IP processing disabled.");
                trustedProxyChainLimit = 0;
            } else if (trustedProxyChainLimit > 0) {
                if (TrustedProxies.Count == 0) {
                    warnings.Add("No trusted proxies are configured. Forwarded client IP processing disabled.");
                    trustedProxyChainLimit = 0;
                } else {
                    var invalidEntries = false;

                    foreach (var entry in TrustedProxies) {
                        try {
                            trustedProxies.AddRange(RunningIpRange.Parse(entry).ToNetworks());
                        } catch (IpRangeParseException ex) {
                            errors.Add($"Invalid trusted proxy entry '{entry}'. {ex.Message}");
                            invalidEntries = true;
                        }
                    }

                    if (invalidEntries) {
                        errors.Add("Forwarded client IP processing disabled because the trusted proxy configuration contains invalid entries.");
                        trustedProxies.Clear();
                        trustedProxyChainLimit = 0;
                    }
                }
            }

            var unmatchedResponse = UnmatchedResponse.AsRunning(warnings.Add);

            return new RunningConfig {
                Revision = Revision,
                ConfigHash = GenerateConfigHash(),
                CommittedAt = CommittedAt ?? DateTime.MinValue,
                Warnings = warnings.ToImmutableArray(),
                Errors = errors.ToImmutableArray(),
                AdminListenAny = AdminListenAny,
                AdminPort = AdminPort,
                AdminCert = AdminCertId,
                MaxRequestBodySize = MaxRequestBodySize,
                UnmatchedResponse = unmatchedResponse,
                HttpPorts = httpPorts.ToImmutableHashSet(),
                HttpsPorts = httpsPorts.ToImmutableHashSet(),
                ProxyPorts = httpPorts.Union(httpsPorts).ToImmutableHashSet(),
                FallbackCert = FallbackCertId,
                SniMaps = new HostTrie(SniMaps),
                Routes = validRoutes.Select(_ => _.ToYarp()).ToImmutableArray(),
                Clusters = clusters.ToImmutableArray(),
                RoutePorts = validRoutes
                    .ToImmutableDictionary(
                        _ => _.Id.ToString(),
                        _ => new PortBinding(_.HttpPort, _.HttpsPort, _.HttpsPort == null ? true : _.SuppressHttpsRedirect)),
                RouteIpRestrictions = routeIpRestrictions.ToImmutableDictionary(),
                NonClusterRoutes = nonClusterRoutes.ToImmutableDictionary(),
                TrustedProxies = trustedProxies.Distinct().ToImmutableArray(),
                TrustedProxyChainLimit = trustedProxyChainLimit
            };
        }

        public ProxyConfig Clone(int newRevision) {
            return new ProxyConfig {
                Revision = newRevision,
                BasedOnRevision = Revision,
                Committed = false,
                Confirmed = false,
                Reverted = false,
                ReversionReason = null,
                CommittedAt = null,
                ConfirmSeconds = ConfirmSeconds,
                AdminListenAny = AdminListenAny,
                AdminPort = AdminPort,
                AdminCertId = AdminCertId,
                Routes = Routes.Select(_ => _.Clone(newRevision)).ToList(),
                Clusters = Clusters.Select(_ => _.Clone(newRevision)).ToList(),
                SniMaps = SniMaps.Select(_ => _.Clone(newRevision)).ToList(),
                FallbackCertId = FallbackCertId,
                MaxRequestBodySize = MaxRequestBodySize,
                UnmatchedResponse = UnmatchedResponse.Clone(),
                TrustedProxies = TrustedProxies.ToList(),
                TrustedProxyChainLimit = TrustedProxyChainLimit,
            };
        }

        public SystemSettingsExport SystemSettingsToExport(bool includeCertificates) {
            return new SystemSettingsExport {
                AdminListenAny = AdminListenAny,
                AdminPort = AdminPort,
                AdminCertId = includeCertificates ? AdminCertId : null,
                FallbackCertId = includeCertificates ? FallbackCertId : null,
                MaxRequestBodySize = MaxRequestBodySize,
                UnmatchedResponse = UnmatchedResponse.ToExport(),
                TrustedProxies = TrustedProxyChainLimit > 0 ? TrustedProxies.Order(StringComparer.Ordinal).ToList() : null,
                TrustedProxyChainLimit = TrustedProxyChainLimit > 0 ? TrustedProxyChainLimit : null,
            };
        }

        private ComparableConfig GenerateComparable(bool sortByOrder) {
            return new ComparableConfig {
                SystemSettings = SystemSettingsToExport(true),
                Routes = (
                    sortByOrder
                        ? Routes.OrderBy(_ => _.Order)
                        : Routes.OrderBy(_ => _.Id))
                    .Select(_ => _.ToExport())
                    .ToList(),
                Clusters = Clusters
                    .OrderBy(_ => _.Id)
                    .Select(_ => _.ToExport(sortByOrder))
                    .ToList(),
                SniMaps = SniMaps
                    .OrderByHost()
                    .Select(_ => _.ToExport())
                    .ToList()
            };
        }

        public string GenerateConfigHash() {
            var comparable = GenerateComparable(false);

            var json = JsonSerializer.Serialize(comparable, new JsonSerializerOptions {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                WriteIndented = false
            });

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        }

        public string GenerateComparableJson(bool sortByOrder) {
            var comparable = GenerateComparable(sortByOrder);

            var json = JsonSerializer.Serialize(comparable, new JsonSerializerOptions {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            return json;
        }        
    }

    public class ComparableConfig {
        public SystemSettingsExport SystemSettings { get; set; }
        public List<RouteExport> Routes { get; set; }
        public List<ClusterExport> Clusters { get; set; }
        public List<SniMapExport> SniMaps { get; set; }
    }

    public class SystemSettingsExport {
        public bool AdminListenAny { get; set; }
        public int AdminPort { get; set; }
        public Guid? AdminCertId { get; set; }
        public Guid? FallbackCertId { get; set; }
        public long? MaxRequestBodySize { get; set; }
        public StaticResponseExport UnmatchedResponse { get; set; }
        public List<string>? TrustedProxies { get; set; }
        public int? TrustedProxyChainLimit { get; set; }
    }
}
