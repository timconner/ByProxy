namespace ByProxy.Data {
    public static class ProxyDbExtensions {
        public static async Task<ProxyConfig?> SelectLatestRunningConfig(this IQueryable<ProxyConfig> query) {
            return await query
                .IgnoreQueryFilters()
                .Where(_ => _.Committed == true && _.Reverted == false)
                .OrderByDescending(_ => _.Revision)
                .FirstOrDefaultAsync();
        }

        public static async Task<ProxyConfig?> SelectCandidateConfig(this IQueryable<ProxyConfig> query) {
            return await query
                .Where(_ => _.Committed == false && _.Reverted == false)
                .OrderByDescending(_ => _.Revision)
                .FirstOrDefaultAsync();
        }

        public static async Task<ProxyConfig?> SelectSpecificRevision(this IQueryable<ProxyConfig> query, int revision) {
            return await query
                .IgnoreQueryFilters()
                .Where(_ => _.Revision == revision)
                .FirstOrDefaultAsync();
        }

        public static async Task<int> GetNextAvailableRevision(this IQueryable<ProxyConfig> query) {
            return await (query.MaxAsync(_ => _.Revision)) + 1;
        }

        public static IQueryable<T> WhereIsActive<T>(this IQueryable<T> query, bool isActive = true) where T : ProxyCert {
            return query.Where(_ => _.Hidden != isActive);
        }
    }
}
