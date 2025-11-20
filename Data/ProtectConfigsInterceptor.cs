namespace ByProxy.Data {

    public class ProtectConfigsInterceptor : SaveChangesInterceptor {
        private readonly IProxyStateService _proxyState;

        public ProtectConfigsInterceptor(IProxyStateService proxyState) {
            _proxyState = proxyState;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) {
            CheckForForbiddenChanges(eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            CheckForForbiddenChanges(eventData);
            return ValueTask.FromResult(result);
        }

        private void CheckForForbiddenChanges(DbContextEventData eventData) {
            if (eventData.Context == null) throw new Exception("Context is not defined.");
            var db = (ProxyDb)eventData.Context;

            foreach (var entry in db.ChangeTracker.Entries()) {
                if (entry.Entity is IVersioned entity) {
                    if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) {
                        if (entry.State == EntityState.Modified && entry.Property(nameof(IVersioned.ConfigRevision)).IsModified) {
                            throw new InvalidOperationException("ConfigRevision cannot be modified.");
                        }
                        if (entity.ConfigRevision != _proxyState.CandidateConfigRevision) {
                            throw new InvalidOperationException("Only the current candidate configuration can be modified.");
                        }
                    }
                } else if (entry.Entity is ProxyConfig config) {
                    if (entry.State == EntityState.Modified && entry.Property(nameof(ProxyConfig.Revision)).IsModified) {
                        throw new InvalidOperationException("ProxyConfig Revision cannot be modified.");
                    }
                }
            }
        }
    }
}
