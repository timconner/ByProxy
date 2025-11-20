namespace ByProxy.Infrastructure.Acme {
    public class AcmeDirectory {
        public required string NewNonce { get; init; }
        public required string NewAccount { get; init; }
        public required string NewOrder { get; init; }
        public string? NewAuthz { get; init; }
        public required string RevokeCert { get; init; }
        public required string KeyChange { get; init; }
        public AcmeMeta? Meta { get; init; }
    }

    public class AcmeMeta {
        public string? TermsOfService { get; init; }
        public string? Website { get; init; }
        public string[]? CaaIdentities { get; init; }
        public bool? ExternalAccountRequired { get; init; }
    }
}
