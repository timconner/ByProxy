namespace ByProxy.AdminApp.Utility {
    public static class ClusterExtensions {
        public static void ApplySort(this List<ProxyCluster> clusters) {
            clusters.Sort((a, b) => {
                var result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (result != 0) return result;
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
        }
    }
}
