namespace ByProxy.Infrastructure {
    public static class Scripting {
        public static ImmutableArray<string> AvailableImports = [
            "System",
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Collections.Generic",
            "System.Collections.Immutable",
            "System.Collections.ObjectModel",
            "System.Globalization",
            "System.Linq",
            "System.Net",
            "System.Net.Http",
            "System.Net.Http.Headers",
            "System.Security.Cryptography",
            "System.Security.Cryptography.X509Certificates",
            "System.Text",
            "System.Text.Json",
            "System.Text.Json.Serialization",
            "System.Text.RegularExpressions",
            "System.Threading.Tasks"
        ];

        private static readonly Lazy<ImmutableArray<Assembly>> _requiredAssemblies = new(() => {
            var assemblies = new List<Assembly> {
                typeof(object).Assembly,                    // System.Private.CoreLib
                typeof(Task).Assembly,                      // System.Runtime
                typeof(List<>).Assembly,                    // System.Collections
                typeof(ImmutableArray).Assembly,            // System.Collections.Immutable
                typeof(Enumerable).Assembly,                // System.Linq
                typeof(IPAddress).Assembly,                 // System.Net.Primitives
                typeof(HttpClient).Assembly,                // System.Net.Http
                typeof(SHA256).Assembly,                    // System.Security.Cryptography
                typeof(JsonSerializer).Assembly,            // System.Text.Json
                typeof(Regex).Assembly,                     // System.Text.RegularExpressions
                typeof(Encoding).Assembly                   // System.Text.Encoding
            };
            return assemblies.Distinct().ToImmutableArray();
        });

        public static ImmutableArray<Assembly> RequiredAssemblies => _requiredAssemblies.Value;
    }
}
