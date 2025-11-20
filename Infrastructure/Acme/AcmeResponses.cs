using System.Diagnostics.Contracts;

namespace ByProxy.Infrastructure.Acme {
    public class AcmeOrderResponse {
        public string OrderUrl { get; init; }
        public AcmeOrder Order { get; init; }

        public AcmeOrderResponse(string orderUrl, AcmeOrder order) {
            OrderUrl = orderUrl;
            Order = order;
        }
    }

    public class AcmeOrder {
        public required string Status { get; init; }
        public DateTime? Expires { get; init; }
        public required AcmeIdentifier[] Identifiers { get; init; }
        public DateTime? NotBefore { get; init; }
        public DateTime? NotAfter { get; init; }
        public AcmeProblem? Error { get; init; }
        public required string[] Authorizations { get; init; }
        public required string Finalize { get; init; }
        public string? Certificate { get; init; }
    }

    public class AcmeAuthorization {
        public required string Status { get; init; }
        public required AcmeIdentifier Identifier { get; init; }
        public required IReadOnlyList<AcmeChallenge> Challenges { get; init; }
        public bool? Wildcard { get; init; }
    }

    public class AcmeChallenge {
        public required string Type { get; init; }
        public required string Url { get; init; }
        public required string Status { get; init; }
        public DateTime? Validated { get; init; }
        public AcmeProblem? Error { get; init; }
        public required string Token { get; init; }
    }
}
