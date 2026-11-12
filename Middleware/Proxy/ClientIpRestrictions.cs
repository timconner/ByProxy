namespace ByProxy.Middleware.Proxy {
    public class ClientIpRestrictions : IMiddleware {
        private readonly ProxyStateService _proxyState;

        public ClientIpRestrictions(
            ProxyStateService proxyState
        ) {
            _proxyState = proxyState;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
            var proxyFeature = context.GetReverseProxyFeature();

            if (!_proxyState.RunningConfig.RouteIpRestrictions.TryGetValue(proxyFeature.Route.Config.RouteId, out var restriction)) {
                await next(context);
                return;
            }

            if (restriction.IsAllowed(context.Connection.RemoteIpAddress)) {
                await next(context);
                return;
            }

            await restriction.DenyResponse.WriteResponseAsync(context);
        }
    }
}
