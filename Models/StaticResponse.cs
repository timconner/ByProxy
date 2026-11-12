namespace ByProxy.Models {
    public class StaticResponse {
        public int HttpStatusCode { get; set; }
        public bool UseTarpit { get; set; }
        public string? Body { get; set; }
        public Dictionary<string, string>? Headers { get; set; }

        public static StaticResponse CreateDefault(int statusCode) {
            return new StaticResponse { HttpStatusCode = statusCode };
        }

        public static StaticResponse CreateTarpit(int statusCode) {
            return new StaticResponse {
                HttpStatusCode = statusCode,
                UseTarpit = true
            };
        }

        public static StaticResponse CreateRedirect(int statusCode, string location) {
            return new StaticResponse {
                HttpStatusCode = statusCode,
                UseTarpit = false,
                Headers = new Dictionary<string, string> {
                    ["Location"] = location
                }
            };
        }

        public RunningStaticResponse AsRunning(Action<string>? warningAction = null) {
            Dictionary<string, string>? headers = null;

            if (Headers != null) {
                headers = new(StringComparer.OrdinalIgnoreCase);

                foreach (var header in Headers) {
                    if (
                        string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                    ) {
                        warningAction?.Invoke($"Ignoring server-managed response header: {header.Key}");
                        continue;
                    }

                    headers[header.Key] = header.Value;
                }
            }

            return new RunningStaticResponse {
                HttpStatusCode = 
                    Enum.IsDefined(typeof(HttpStatusCode), HttpStatusCode)
                        ? HttpStatusCode
                        : 404,
                UseTarpit = UseTarpit,
                Body = Body == null ? null : Encoding.UTF8.GetBytes(Body),
                Headers = headers?.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase)
            };
        }

        public StaticResponse Clone() {
            return new StaticResponse {
                HttpStatusCode = HttpStatusCode,
                UseTarpit = UseTarpit,
                Body = Body,
                Headers = Headers == null ? null : Headers.ToDictionary()
            };
        }

        public StaticResponseExport ToExport() {
            return new StaticResponseExport {
                HttpStatusCode = HttpStatusCode,
                UseTarpit = UseTarpit,
                Body = Body,
                Headers = Headers?
                    .OrderBy(header => header.Key, StringComparer.Ordinal)
                    .ToDictionary(header => header.Key, header => header.Value)
            };
        }
    }

    public class StaticResponseExport {
        public int HttpStatusCode { get; set; }
        public bool UseTarpit { get; set; }
        public string? Body { get; set; }
        public Dictionary<string, string>? Headers { get; set; }

        public StaticResponse Realize() {
            return new StaticResponse {
                HttpStatusCode = HttpStatusCode,
                UseTarpit = UseTarpit,
                Body = Body,
                Headers = Headers?.ToDictionary()
            };
        }
    }
}
