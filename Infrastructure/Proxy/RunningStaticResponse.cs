namespace ByProxy.Infrastructure.Proxy {
    public class RunningStaticResponse {
        public required int HttpStatusCode { get; init; }
        public required bool UseTarpit { get; init; }
        public ReadOnlyMemory<byte>? Body { get; init; }
        public IReadOnlyDictionary<string, string>? Headers { get; init; }
    }

    public static class RunningStaticResponseExtensions {
        public static async Task WriteResponseAsync(this RunningStaticResponse response, HttpContext context) {
            if (response.UseTarpit) {
                var tarpitTime = Random.Shared.Next(15_000, 30_000);
                try {
                    await Task.Delay(tarpitTime, context.RequestAborted);
                } catch { return; }
            }
            context.Response.StatusCode = response.HttpStatusCode;

            if (response.Headers != null) {
                foreach (var header in response.Headers) {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }

            if (response.HttpStatusCode < 200 || response.HttpStatusCode is 204 or 205 or 304) {
                return; // These statuses do not allow bodies
            }

            if (response.Body != null) {
                context.Response.ContentType ??= "text/html; charset=utf-8";
                context.Response.ContentLength = response.Body.Value.Length;

                if (!HttpMethods.IsHead(context.Request.Method)) {
                    await context.Response.BodyWriter.WriteAsync(response.Body.Value, context.RequestAborted);
                }
            } else {
                context.Response.ContentLength = 0;
            }
        }
    }
}
