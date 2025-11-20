namespace ByProxy.Middleware.Proxy {
    public class ConditionalHttpsRedirect : IMiddleware {
        private readonly ILogger<ConditionalHttpsRedirect> _logger;
        private readonly ProxyStateService _proxyState;

        public ConditionalHttpsRedirect(
            ILogger<ConditionalHttpsRedirect> logger,
            ProxyStateService proxyState
        ) {
            _logger = logger;
            _proxyState = proxyState;
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next) {
            if (!_proxyState.RunningConfig.HttpPorts.Contains(context.Connection.LocalPort)) return next(context);

            var proxyFeature = context.GetReverseProxyFeature();
            if (!_proxyState.RunningConfig.RoutePorts.TryGetValue(proxyFeature.Route.Config.RouteId, out var ports)) return next(context);

            if (
                ports.SuppressRedirect
                || context.Connection.LocalPort != ports.HttpPort
                || ports.HttpsPort == null
            ) {
                return next(context);
            }
            var request = context.Request;

            HostString host;
            if (ports.HttpsPort != 443) {
                host = new HostString(request.Host.Host, ports.HttpsPort.Value);
            } else {
                host = new HostString(request.Host.Host);
            }

            var redirectUrl = UriHelper.BuildAbsolute(
                "https",
                host,
                request.PathBase,
                request.Path,
                request.QueryString);

            _logger.LogInformation($"Redirecting {context.Request.Host.Host}:{context.Connection.LocalPort} -> {redirectUrl}");

            context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            context.Response.Headers.Location = redirectUrl;

            return Task.CompletedTask;
        }
    }
}
