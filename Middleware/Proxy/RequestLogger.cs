namespace ByProxy.Middleware.Proxy {
    public class RequestLogger : IMiddleware {
        private readonly ILogger<RequestLogger> _logger;

        public RequestLogger(
            ILogger<RequestLogger> logger
        ) {
            _logger = logger;
        }

        private static string NormalizeIP(IPAddress? ip) {
            if (ip == null) return "--";
            if (ip.IsIPv4MappedToIPv6) return ip.MapToIPv4().ToString();
            return ip.ToString();
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
            var stopwatch = Stopwatch.StartNew();
            try {
                await next(context);
            } finally {
                stopwatch.Stop();

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
                var protocol = context.Request.Protocol;
                var method = context.Request.Method;
                var statusCode = context.Response.StatusCode;
                var contentLength = context.Response.ContentLength;
                var contentType = context.Response.ContentType;

                _logger.LogInformation(
                    "{RemoteIp} {Protocol} {Method} {Url} - {StatusCode} {ContentLength} {ContentType} {ElapsedMs}ms",
                    remoteIps,
                    protocol ?? "--",
                    method ?? "--",
                    url ?? "--",
                    statusCode,
                    contentLength?.ToString() ?? "--",
                    contentType ?? "--",
                    stopwatch.Elapsed.TotalMilliseconds
                );
            }
        }
    }
}
