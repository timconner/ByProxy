namespace ByProxy.Middleware.Proxy {
    public class AcmeChallengeIntercept : IMiddleware {
        private readonly ILogger<AcmeChallengeIntercept> _logger;
        private readonly AcmeClientService _acmeClient;

        public AcmeChallengeIntercept(
            ILogger<AcmeChallengeIntercept> logger,
            AcmeClientService acmeClient
        ) {
            _logger = logger;
            _acmeClient = acmeClient;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
            if (!context.Request.Path.StartsWithSegments("/.well-known/acme-challenge", out var remainder)) {
                await next(context);
                return;
            }

            var token = remainder.Value?.TrimStart('/');
            if (token == null) {
                await next(context);
                return;
            }

            if (!_acmeClient.PendingHttp01Challenges.TryGetValue(token, out var challenge)) {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength = challenge.KeyAuthorization.Length;
            await context.Response.Body.WriteAsync(challenge.KeyAuthorization);

            challenge.TaskCompletionSource.TrySetResult();
        }
    }
}
