namespace ByProxy.Infrastructure.Proxy {
    public sealed record PortBinding(int? HttpPort, int? HttpsPort, bool SuppressRedirect);

    public abstract class NonClusterRoute();

    public sealed class StaticResponseRoute : NonClusterRoute {
        public required RunningStaticResponse Response { get; set; }
    }


    public sealed class RunningConfig {
        public required int Revision { get; init; }
        public required string ConfigHash { get; init; }
        public required DateTime CommittedAt { get; init; }
        
        public required ImmutableArray<string> Warnings { get; init; }
        public required ImmutableArray<string> Errors { get; init; }

        public required bool AdminListenAny { get; init; }
        public required int AdminPort { get; init; }
        public required Guid AdminCert { get; init; }

        public required long? MaxRequestBodySize { get; init; }
        public required RunningStaticResponse UnmatchedResponse { get; init; }

        public required ImmutableHashSet<int> HttpPorts { get; init; }
        public required ImmutableHashSet<int> HttpsPorts { get; init; }
        public required ImmutableHashSet<int> ProxyPorts { get; init; }
        public required ImmutableDictionary<string, PortBinding> RoutePorts { get; init; }
        public required ImmutableDictionary<string, RunningIpRestriction> RouteIpRestrictions { get; init; }

        public required ImmutableArray<RouteConfig> Routes { get; init; }
        public required ImmutableArray<ClusterConfig> Clusters { get; init; }

        public required ImmutableDictionary<string, NonClusterRoute> NonClusterRoutes { get; init; }

        public required Guid? FallbackCert { get; init; }
        public required HostTrie SniMaps { get; init; }

        public required ImmutableArray<System.Net.IPNetwork> TrustedProxies { get; init; }
        public required int TrustedProxyChainLimit { get; init; }
    }
}
