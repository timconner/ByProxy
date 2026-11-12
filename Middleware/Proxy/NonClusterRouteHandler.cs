namespace ByProxy.Middleware.Proxy {
    public class NonClusterRouteHandler : IMiddleware {
        private readonly ProxyStateService _proxyState;

        public NonClusterRouteHandler(
            ProxyStateService proxyState
        ) {
            _proxyState = proxyState;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
            var proxyFeature = context.GetReverseProxyFeature();
            if (!_proxyState.RunningConfig.NonClusterRoutes.TryGetValue(proxyFeature.Route.Config.RouteId, out var ncRoute)) {
                await next(context);
                return;
            }

            if (ncRoute is StaticResponseRoute staticRoute) {
                await staticRoute.Response.WriteResponseAsync(context);
            } else {
                throw new NotImplementedException("Unknown NonCluster Route Type");
            }
        }
    }
}
