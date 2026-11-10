namespace ByProxy.Middleware.Proxy {
    public class RequestLogger : IMiddleware {
        private readonly ILogger<RequestLogger> _logger;

        public RequestLogger(
            ILogger<RequestLogger> logger
        ) {
            _logger = logger;
        }

        private const string UNKNOWN_VALUE = "--";

        private static string NormalizeIP(IPAddress? ip) {
            if (ip == null) return UNKNOWN_VALUE;
            if (ip.IsIPv4MappedToIPv6) return ip.MapToIPv4().ToString();
            return ip.ToString();
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
            var stopwatch = Stopwatch.StartNew();
            
            var responseStream = context.Response.Body;
            using var streamCounter = new StreamCounter(responseStream);
            context.Response.Body = streamCounter;
            
            try {
                await next(context);
            } finally {
                stopwatch.Stop();
                context.Response.Body = responseStream;

                var ips = new List<string>();
                if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var xff)) {
                    foreach (var ip in xff.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) {
                        if (IPAddress.TryParse(ip, out var parsedIp)) {
                            ips.Add(NormalizeIP(parsedIp));
                        } else {
                            ips.Add(ip);
                        }
                    }
                }
                ips.Add(NormalizeIP(context.Connection.RemoteIpAddress));
                var remoteIps = string.Join('|', ips);

                var url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                var protocol = context.Request.Protocol ?? UNKNOWN_VALUE;
                var method = context.Request.Method ?? UNKNOWN_VALUE;
                var contentType = context.Response.ContentType?.Split(';',2)[0].Trim() ?? UNKNOWN_VALUE;

                var abortedBeforeResponse = context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted;
                var statusCode = abortedBeforeResponse ? 499 : context.Response.StatusCode;
                var responseBytes = abortedBeforeResponse ? UNKNOWN_VALUE : streamCounter.BytesWritten.ToString();

                _logger.LogInformation(
                    "{RemoteIp} {Protocol} {Method} {Url} - {StatusCode} {ResponseBytes} {ContentType} {ElapsedMs}ms",
                     remoteIps,  protocol,  method,  url,    statusCode,  responseBytes,  contentType,  stopwatch.Elapsed.TotalMilliseconds
                );
            }
        }
    }
}
