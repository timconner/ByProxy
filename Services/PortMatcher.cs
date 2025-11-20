namespace ByProxy.Services {
    public class PortMatcher : MatcherPolicy, IEndpointSelectorPolicy {
        private readonly ILogger<PortMatcher> _logger;
        private readonly ProxyStateService _proxyState;

        public PortMatcher(ILogger<PortMatcher> logger, ProxyStateService proxyState) {
            _logger = logger;
            _proxyState = proxyState;
        }

        public override int Order => 101;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) {
            return endpoints.Any(static e => e.Metadata.GetMetadata<RouteModel>() != null);
        }

        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates) {
            var localPort = httpContext.Connection.LocalPort;
            for (int i = 0; i < candidates.Count; i++) {
                if (!candidates.IsValidCandidate(i)) continue;

                var routeModel = candidates[i].Endpoint.Metadata.GetMetadata<RouteModel>();
                if (routeModel == null) continue;

                if (!_proxyState.RunningConfig.RoutePorts.TryGetValue(routeModel.Config.RouteId, out var ports)) continue;

                if (localPort != ports.HttpsPort && localPort != ports.HttpPort) {
                    candidates.SetValidity(i, false);
                }
            }
            return Task.CompletedTask;
        }
    }
}
