namespace ByProxy.Services {
    public class ScriptCompilationService {
        private readonly ILogger<ScriptCompilationService> _logger;
        private readonly IDbContextFactory<ProxyDb> _dbFactory;

        private readonly ConcurrentDictionary<Guid, IAcmeDnsProvider> _dnsProviderCache = new();

        public ScriptCompilationService(
            ILogger<ScriptCompilationService> logger,
            IDbContextFactory<ProxyDb> dbFactory
        ) {
            _logger = logger;
            _dbFactory = dbFactory;
        }

        public async Task<IAcmeDnsProvider> RetrieveAcmeDnsProvider(Guid dnsProviderId) {
            using var db = _dbFactory.CreateDbContext();

            if (!_dnsProviderCache.TryGetValue(dnsProviderId, out IAcmeDnsProvider? provider)) {
                var entity = await db.AcmeDnsProviders
                    .AsNoTracking()
                    .Where(_ => _.Id == dnsProviderId)
                    .FirstAsync();
                if (entity?.Script == null) throw new Exception($"Unable to load ACME DNS Provider script {dnsProviderId} from database.");

                _logger.LogInformation($"Compiling ACME DNS Provider: {entity.Name} ({entity.Id})");
                try {
                    provider = await AcmeDnsScripting.CompileProviderScript(entity.Script);
                } catch (Exception ex) {
                    _logger.LogError($"Failed to compile ACME DNS Provider \"{entity.Name}\" ({entity.Id}): {ex}");
                    throw;
                }
                _dnsProviderCache.TryAdd(dnsProviderId, provider);
            }

            return provider;
        }

        public void RemoveAcmeDnsProviderFromCache(Guid dnsProviderId) {
            _dnsProviderCache.Remove(dnsProviderId, out _);
        }
    }
}
