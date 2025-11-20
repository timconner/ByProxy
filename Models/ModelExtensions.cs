namespace ByProxy.Models {
    public static class ModelExtensions {
        public static IEnumerable<ProxySniMap> OrderByHost(this IEnumerable<ProxySniMap> hostnames) {
            return hostnames
                .OrderBy(map => map.Host.StartsWith("*.") ? map.Host.Substring(2) : map.Host, StringComparer.OrdinalIgnoreCase)
                .ThenBy(map => map.Host.StartsWith("*.") ? 1 : 0);
        }

        public static IQueryable<ProxySniMap> OrderByHost(this IQueryable<ProxySniMap> hostnames) {
            return hostnames
                .OrderBy(map => map.Host.StartsWith("*.") ? map.Host.Substring(2).ToLower() : map.Host.ToLower())
                .ThenBy(map => map.Host.StartsWith("*.") ? 1 : 0);
        }

        public static void SortByHost(this List<ProxySniMap> hostnames) {
            hostnames.Sort((x, y) => {
                string baseX = x.Host.StartsWith("*.") ? x.Host.Substring(2) : x.Host;
                string baseY = y.Host.StartsWith("*.") ? y.Host.Substring(2) : y.Host;

                int baseCompare = string.Compare(baseX, baseY, StringComparison.OrdinalIgnoreCase);
                if (baseCompare != 0) return baseCompare;

                bool xIsWildcard = x.Host.StartsWith("*.");
                bool yIsWildcard = y.Host.StartsWith("*.");
                return xIsWildcard.CompareTo(yIsWildcard);
            });
        }
    }
}
