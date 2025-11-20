namespace ByProxy.Infrastructure.AcmeDnsProvider {
    public interface IAcmeDnsProvider {
        public Task<bool> CreateDnsRecord(string domain, string txtValue);
        public Task<bool> DeleteDnsRecord(string domain, string txtValue);
    }

    public static class AcmeDnsScripting {
        public static async Task<IAcmeDnsProvider> CompileProviderScript(string dnsScript) {
            var assemblies = Scripting.RequiredAssemblies.Add(typeof(IAcmeDnsProvider).Assembly);
            var imports = Scripting.AvailableImports.Add(typeof(IAcmeDnsProvider).Namespace ?? throw new Exception("Failed to determine namespace of IAcmeDnsProvider"));

            var options = ScriptOptions.Default
                .WithReferences(assemblies.Select(_ => MetadataReference.CreateFromFile(_.Location)))
                .WithImports(imports);

            var script = CSharpScript.Create<IAcmeDnsProvider>(dnsScript, options);
            var diagnostics = script.Compile();

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0) {
                var errorMessages = errors.Select(e =>
                    $"Line {e.Location.GetLineSpan().StartLinePosition.Line + 1}: {e.GetMessage()}");
                throw new InvalidOperationException(
                    $"Compilation failed:\n{string.Join("\n", errorMessages)}");
            }
            
            var state = await script.RunAsync();
            if (state.Exception != null) {
                throw new InvalidOperationException($"Script execution failed: {state.Exception.Message}", state.Exception);
            }
            if (state.ReturnValue == null) {
                throw new InvalidOperationException("Script must return an instance of IAcmeDnsProvider");
            }

            return state.ReturnValue;
        }

        public const string DNS_PROVIDER_TEMPLATE = @"public class AcmeDnsProvider : IAcmeDnsProvider {
    private readonly HttpClient _http;

    public AcmeDnsProvider() {
        _http = new HttpClient();
    }

    public async Task<bool> CreateDnsRecord(string domain, string txtValue) {
        // Create DNS record to satisfy ACME challenge.
        return false;
    }

    public async Task<bool> DeleteDnsRecord(string domain, string txtValue) {
        // Cleanup DNS record after challenge completes.
        return false;
    }
}

return new AcmeDnsProvider();";
    }
}

