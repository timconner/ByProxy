namespace ByProxy.Infrastructure {
    public static class AppPaths {
        public static string AppDir => AppContext.BaseDirectory;

        public static string DataDir => _dataDir.Value;

        private static readonly Lazy<string> _dataDir = new(() => {
            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var dataDir = config["Proxy:DataDir"];

            if (!string.IsNullOrWhiteSpace(dataDir)) {
                if (!Directory.Exists(dataDir)) throw new Exception($"Specified DataDir does not exist: {dataDir}");
            } else {
                var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
                if (baseDir.Parent == null) throw new DirectoryNotFoundException("Unable to detect parent directory for server binary.");
                dataDir = Path.Combine(baseDir.Parent.FullName, "data");
                Directory.CreateDirectory(dataDir);
            }
            return dataDir;
        });

        public static string CertDir => _certDir.Value;
        private static readonly Lazy<string> _certDir = new(() => {
            var certDir = Path.Combine(DataDir, "certs");
            Directory.CreateDirectory(certDir);
            return certDir;
        });

        public static string AcmeAccountDir => _acmeAccountDir.Value;
        private static readonly Lazy<string> _acmeAccountDir = new(() => {
            var acmeAccountsDir = Path.Combine(DataDir, "acme", "accounts");
            Directory.CreateDirectory(acmeAccountsDir);
            return acmeAccountsDir;
        });
    }
}
