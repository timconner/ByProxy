namespace ByProxy.Middleware.Auth {
    public class CookieTicketStore : ITicketStore {
        private readonly ILogger<CookieTicketStore> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        
        public CookieTicketStore(ILogger<CookieTicketStore> logger, IServiceScopeFactory scopeFactory) {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task RemoveAsync(string key) {
            using (var scope = _scopeFactory.CreateScope()) {
                var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
                await auth.InvalidateToken(key);
            }
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket) {
            _logger.LogWarning("Token Renewal Not Implemented");
            return Task.CompletedTask;
        }

        public async Task<AuthenticationTicket?> RetrieveAsync(string key) {
            using (var scope = _scopeFactory.CreateScope()) {
                var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
                var result = await auth.AuthenticateToken(CookieAuthenticationDefaults.AuthenticationScheme, key);
                return result.Succeeded ? result.Ticket : null;
            }
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket) {
            using (var scope = _scopeFactory.CreateScope()) {
                var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
                return await auth.CreateToken(ticket);
            }
        }
    }
}
