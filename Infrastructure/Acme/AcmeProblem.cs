namespace ByProxy.Infrastructure.Acme {
    public struct AcmeProblem {
        public required string Type { get; init; }
        public string? Title { get; init; }
        public int? Status { get; init; }
        public string? Detail { get; init; }
        public string? Instance { get; init; }

        public bool IsBadNonce => Type == "urn:ietf:params:acme:error:badNonce";
        public bool IsRateLimit => Type == "urn:ietf:params:acme:error:rateLimited";
    }
}
