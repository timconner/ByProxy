namespace ByProxy.Middleware.Proxy {
    public class NonClusterRouteHandler : IMiddleware {
        private readonly ILogger<NonClusterRouteHandler> _logger;
        private readonly ProxyStateService _proxyState;

        public NonClusterRouteHandler(
            ILogger<NonClusterRouteHandler> logger,
            ProxyStateService proxyState
        ) {
            _logger = logger;
            _proxyState = proxyState;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
            var proxyFeature = context.GetReverseProxyFeature();
            if (!_proxyState.RunningConfig.NonClusterRoutes.TryGetValue(proxyFeature.Route.Config.RouteId, out var ncRoute)) {
                await next(context);
                return;
            }

            if (ncRoute is RedirectRoute redirectRoute) {
                _logger.LogInformation($"Redirecting {context.Request.Host.Host}:{context.Connection.LocalPort} -> {redirectRoute.RedirectLocation}");
                context.Response.StatusCode = redirectRoute.RedirectStatus;
                context.Response.Headers.Location = redirectRoute.RedirectLocation;
            } else if (ncRoute is StaticResponseRoute staticRoute) {
                if (staticRoute.UseTarpit) {
                    var tarpitTime = Random.Shared.Next(15_000, 30_000);
                    try {
                        await Task.Delay(tarpitTime, context.RequestAborted);
                    } catch { return; }
                }
                context.Response.StatusCode = staticRoute.HttpStatusCode;

                if (staticRoute.ResponseHeaders != null) {
                    foreach (var header in staticRoute.ResponseHeaders) {
                        context.Response.Headers[header.Key] = header.Value;
                    }
                }

                if (staticRoute.HttpStatusCode < 200 || staticRoute.HttpStatusCode is 204 or 205 or 304) {
                    return; // These statuses do not allow bodies
                }

                if (staticRoute.ResponseBody != null) {
                    context.Response.ContentType ??= "text/html; charset=utf-8";
                    context.Response.ContentLength = staticRoute.ResponseBody.Value.Length;
                    
                    if (!HttpMethods.IsHead(context.Request.Method)) {
                        await context.Response.BodyWriter.WriteAsync(staticRoute.ResponseBody.Value, context.RequestAborted);
                    }
                } else {
                    context.Response.ContentLength = 0;
                }
            } else {
                throw new NotImplementedException("Unknown NonCluster Route Type");
            }
        }
    }
}
