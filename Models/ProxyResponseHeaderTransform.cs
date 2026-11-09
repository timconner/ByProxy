namespace ByProxy.Models {
    public class ProxyResponseHeaderTransform : ProxyResponseTransform {
        public override ResponseTransformType TransformType => ResponseTransformType.Header;

        public ResponseHeaderTransformOperation HeaderOperation { get; set; }
        public string HeaderName { get; set; }
        public string? HeaderValue { get; set; }

        public override ProxyResponseTransform Clone(int newRevision) {
            return new ProxyResponseHeaderTransform {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                HeaderOperation = HeaderOperation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }

        public override ResponseTransformExport ToExport() {
            return new ResponseHeaderTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = HeaderOperation.Operation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }
    }

    public class ResponseHeaderTransformExport : ResponseTransformExport {
        public string Operation { get; set; }
        public string HeaderName { get; set; }
        public string? HeaderValue { get; set; }

        public override ProxyResponseTransform Realize(int revision) {
            return new ProxyResponseHeaderTransform {
                Id = Id,
                ConfigRevision = revision,
                RouteId = RouteId,
                RouteConfigRevision = revision,
                HeaderOperation = ResponseHeaderTransformOperation.FromString(Operation) ?? throw new NotImplementedException($"Unknown Header Transform Operation: {Operation}"),
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }
    }

    public record ResponseHeaderTransformOperation(string Operation) {
        public static class Constants {
            public const string Set = "Set";
            public const string Append = "Append";
            public const string Remove = "Remove";
        }

        public static readonly ResponseHeaderTransformOperation Set = new(Constants.Set);
        public static readonly ResponseHeaderTransformOperation Append = new(Constants.Append);
        public static readonly ResponseHeaderTransformOperation Remove = new(Constants.Remove);

        public static ImmutableArray<ResponseHeaderTransformOperation> HeaderTransformOperations = [Set, Append, Remove];

        public static ResponseHeaderTransformOperation? FromString(string? value) => value switch {
            Constants.Set => Set,
            Constants.Append => Append,
            Constants.Remove => Remove,
            _ => null
        };

        public class HeaderTransformOperationConverter : ValueConverter<ResponseHeaderTransformOperation?, string?> {
            public HeaderTransformOperationConverter() : base(
                headerTransformOperation => headerTransformOperation == null ? null : headerTransformOperation.Operation,
                    value => FromString(value)) { }
        }
    }
}
