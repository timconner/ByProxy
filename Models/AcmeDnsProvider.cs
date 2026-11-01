namespace ByProxy.Models {
    public class AcmeDnsProvider {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Draft { get; set; }
        public string? Script { get; set; }

        public AcmeDnsProviderExport ToExport() {
            return new AcmeDnsProviderExport {
                Id = Id,
                Name = Name,
                Script = Script
            };
        }
    }

    public class AcmeDnsProviderExport {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Script { get; set; }
    }
}
