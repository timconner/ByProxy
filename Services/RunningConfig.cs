namespace ByProxy.Services {
    public sealed record PortBinding(int? HttpPort, int? HttpsPort, bool SuppressRedirect);
    
    public abstract class NonClusterRoute();
    public sealed class RedirectRoute : NonClusterRoute {
        public required int RedirectStatus { get; init; }
        public required string RedirectLocation { get; init; }
    }
    public sealed class StatusResponseRoute : NonClusterRoute {
        public required int HttpStatusCode { get; init; }
        public required bool UseTarpit { get; init; }
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

        public required int UnmatchedStatus { get; init; }
        public required bool Tarpit { get; init; }

        public required ImmutableHashSet<int> HttpPorts { get; init; }
        public required ImmutableHashSet<int> HttpsPorts { get; init; }
        public required ImmutableHashSet<int> ProxyPorts { get; init; }
        public required ImmutableDictionary<string, PortBinding> RoutePorts { get; init; }

        public required ImmutableArray<RouteConfig> Routes { get; init; }
        public required ImmutableArray<ClusterConfig> Clusters { get; init; }

        public required ImmutableDictionary<string, NonClusterRoute> NonClusterRoutes { get; init; }

        public required Guid? FallbackCert { get; init; }
        public required HostTrie SniMaps { get; init; }
    }
}
