namespace ByProxy.Models {
    public class ProxyRequestHeaderTransform : ProxyRequestTransform {
        public override RequestTransformType TransformType => RequestTransformType.Header;

        public RequestHeaderTransformOperation HeaderOperation { get; set; }
        public string HeaderName { get; set; }
        public string? HeaderValue { get; set; }

        public override ProxyRequestTransform Clone(int newRevision) {
            return new ProxyRequestHeaderTransform {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                HeaderOperation = HeaderOperation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }

        public override RequestTransformExport ToExport() {
            return new RequestHeaderTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = HeaderOperation.Operation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }
    }

    public class RequestHeaderTransformExport : RequestTransformExport {
        public string Operation { get; set; }
        public string HeaderName { get; set; }
        public string? HeaderValue { get; set; }

        public override ProxyRequestTransform Realize(int revision) {
            return new ProxyRequestHeaderTransform {
                Id = Id,
                ConfigRevision = revision,
                RouteId = RouteId,
                RouteConfigRevision = revision,
                HeaderOperation = RequestHeaderTransformOperation.FromString(Operation) ?? throw new NotImplementedException($"Unknown Header Transform Operation: {Operation}"),
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }
    }

    public record RequestHeaderTransformOperation(string Operation) {
        public static class Constants {
            public const string Set = "Set";
            public const string Append = "Append";
            public const string Remove = "Remove";
            public const string RouteSet = "RouteSet";
            public const string RouteAppend = "RouteAppend";
        }

        public static readonly RequestHeaderTransformOperation Set = new(Constants.Set);
        public static readonly RequestHeaderTransformOperation Append = new(Constants.Append);
        public static readonly RequestHeaderTransformOperation Remove = new(Constants.Remove);
        public static readonly RequestHeaderTransformOperation RouteSet = new(Constants.RouteSet);
        public static readonly RequestHeaderTransformOperation RouteAppend = new(Constants.RouteAppend);

        public static ImmutableArray<RequestHeaderTransformOperation> HeaderTransformOperations = [Set, Append, Remove, RouteSet, RouteAppend];

        public static RequestHeaderTransformOperation? FromString(string? value) => value switch {
            Constants.Set => Set,
            Constants.Append => Append,
            Constants.Remove => Remove,
            Constants.RouteSet => RouteSet,
            Constants.RouteAppend => RouteAppend,
            _ => null
        };

        public class HeaderTransformOperationConverter : ValueConverter<RequestHeaderTransformOperation?, string?> {
            public HeaderTransformOperationConverter() : base(
                headerTransformOperation => headerTransformOperation == null ? null : headerTransformOperation.Operation,
                    value => FromString(value)) { }
        }
    }
}
