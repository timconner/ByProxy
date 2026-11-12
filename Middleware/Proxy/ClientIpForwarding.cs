namespace ByProxy.Middleware.Proxy {
    public sealed class ClientIpForwarding : IMiddleware, IDisposable {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ProxyStateService _proxyState;
        private readonly object __lock = new();

        private ForwardedHeadersMiddleware? _forwardedHeadersMiddleware;

        public ClientIpForwarding(
            ILoggerFactory loggerFactory,
            ProxyStateService proxyState
        ) {
            _loggerFactory = loggerFactory;
            _proxyState = proxyState;

            _proxyState.OnConfigChange += ConfigurationChanged;

            try {
                Refresh();
            } catch {
                _proxyState.OnConfigChange -= ConfigurationChanged;
                throw;
            }
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next) {
            var currentMiddleware = Volatile.Read(ref _forwardedHeadersMiddleware);

            currentMiddleware?.ApplyForwarders(context);

            return next(context);
        }

        private void ConfigurationChanged(object? sender, EventArgs e) {
            Refresh();
        }

        private void Refresh() {
            lock (__lock) {
                var config = _proxyState.RunningConfig;

                if (config.TrustedProxyChainLimit <= 0 || config.TrustedProxies.IsDefaultOrEmpty) {
                    Volatile.Write(ref _forwardedHeadersMiddleware, null);
                    return;
                }

                var options = new ForwardedHeadersOptions {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor,
                    ForwardLimit = config.TrustedProxyChainLimit
                };

                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();

                foreach (var network in config.TrustedProxies) {
                    options.KnownIPNetworks.Add(network);
                }

                var newMiddleware = new ForwardedHeadersMiddleware(
                    _ => Task.CompletedTask,
                    _loggerFactory,
                    Options.Create(options));

                Volatile.Write(ref _forwardedHeadersMiddleware, newMiddleware);
            }
        }

        public void Dispose() {
            _proxyState.OnConfigChange -= ConfigurationChanged;
        }
    }
}

