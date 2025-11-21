using ByProxy.Infrastructure.Acme;

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
        public int UnmatchedStatus { get; set; }
        public bool Tarpit { get; set; }

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
                        if (route.HttpStatusCode == null || route.RedirectLocation == null) {
                            errors.Add($"Route {route.Name} ejected. Missing required properties for a redirect route.");
                            error = true;
                        } else {
                            nonClusterRoutes.Add(route.Id.ToString(), new RedirectRoute {
                                RedirectStatus = route.HttpStatusCode.Value,
                                RedirectLocation = route.RedirectLocation 
                            });
                        }
                        break;
                    case RouteResponseType.Constants.Status:
                    case RouteResponseType.Constants.Tarpit:
                        if (route.HttpStatusCode == null) {
                            errors.Add($"Route {route.Name} ejected. Missing required properties for a redirect route.");
                            error = true;
                        } else {
                            nonClusterRoutes.Add(route.Id.ToString(), new StatusResponseRoute {
                                HttpStatusCode = route.HttpStatusCode.Value,
                                UseTarpit = route.ResponseType == RouteResponseType.Tarpit
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

            int unmatchedStatus;
            if (!Enum.IsDefined(typeof(HttpStatusCode), UnmatchedStatus)) {
                if (Tarpit) {
                    unmatchedStatus = 500;
                } else {
                    unmatchedStatus = 404;
                }
            } else {
                unmatchedStatus = UnmatchedStatus;
            }

            var clusters = Clusters.Select(_ => _.ToYarp()).ToList();
            clusters.Add(ProxyCluster.NullCluster);

            return new RunningConfig {
                Revision = Revision,
                ConfigHash = GenerateConfigHash(),
                CommittedAt = CommittedAt ?? DateTime.MinValue,
                Warnings = warnings.ToImmutableArray(),
                Errors = errors.ToImmutableArray(),
                AdminListenAny = AdminListenAny,
                AdminPort = AdminPort,
                AdminCert = AdminCertId,
                Tarpit = Tarpit,
                UnmatchedStatus = unmatchedStatus,
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
                NonClusterRoutes = nonClusterRoutes.ToImmutableDictionary()
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
                UnmatchedStatus = UnmatchedStatus,
                Tarpit = Tarpit
            };
        }

        private dynamic GenerateComparable(bool sortByOrder) {
            return new {
                //ConfirmSeconds = ConfirmSeconds, Intentionally Excluded (Set at time of commit)
                AdminListenAny = AdminListenAny,
                AdminPort = AdminPort,
                AdminCertId = AdminCertId,
                FallbackCertId = FallbackCertId,
                UnmatchedStatus = UnmatchedStatus,
                Tarpit = Tarpit,
                Routes = Routes
                    .OrderBy(_ => sortByOrder ? (IComparable)_.Order : (IComparable)_.Id)
                    .Select(_ => _.ToComparable())
                    .ToArray(),
                Clusters = Clusters
                    .OrderBy(_ => _.Id)
                    .Select(_ => _.ToComparable(sortByOrder))
                    .ToArray(),
                SniMaps = SniMaps
                    .OrderByHost()
                    .Select(_ => _.ToComparable())
                    .ToArray()
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

        //private ProxyRoute GenerateRoute(string name, string? hostname, string destination, int httpPort, int httpsPort, AcmeProvider? acmeProvider) {
        //    var id = Guid.NewGuid();

        //    var cluster = new ProxyCluster() {
        //        Id = id,
        //        ConfigRevision = Revision,
        //        Name = name,
        //        Description = string.Empty
        //    };

        //    cluster.Destinations = new List<ProxyDestination>() {
        //        new ProxyDestination() {
        //            Id = id,
        //            ConfigRevision = Revision,
        //            Name = name,
        //            ClusterId = cluster.Id,
        //            Cluster = cluster,
        //            Address = destination
        //        }
        //    };

        //    Clusters.Add(cluster);

        //    return new ProxyRoute {
        //        Id = id,
        //        ConfigRevision = Revision,
        //        Name = name,
        //        Description = string.Empty,
        //        ClusterId = cluster.Id,
        //        Cluster = cluster,
        //        Hosts = hostname == null ? null : [hostname],
        //        Path = null,
        //        HttpPort = httpPort,
        //        HttpsPort = httpsPort
        //    };
        //}

        public void AddSimpleConfig(string hostname, string destination) {
            throw new NotImplementedException("Simple mode ACME integration pending");
            //var route = GenerateRoute("Simple", hostname, destination, 80, 443, AcmeProvider.Providers.FirstOrDefault());
            //Routes.Add(route);
        }
    }
}
