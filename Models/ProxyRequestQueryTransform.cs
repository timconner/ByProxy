namespace ByProxy.Models {
    public class ProxyRequestQueryTransform : ProxyRequestTransform {
        public override RequestTransformType TransformType => RequestTransformType.Query;

        public RequestQueryTransformOperation QueryOperation { get; set; }
        public string QueryKey { get; set; }
        public string? QueryValue { get; set; }

        public override ProxyRequestTransform Clone(int newRevision) {
            return new ProxyRequestQueryTransform {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                QueryOperation = QueryOperation,
                QueryKey = QueryKey,
                QueryValue = QueryValue
            };
        }

        public override RequestTransformExport ToExport() {
            return new RequestQueryTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = QueryOperation.Operation,
                QueryKey = QueryKey,
                QueryValue = QueryValue
            };
        }
    }

    public class RequestQueryTransformExport : RequestTransformExport {
        public string Operation { get; set; }
        public string QueryKey { get; set; }
        public string? QueryValue { get; set; }

        public override ProxyRequestTransform Realize(int revision) {
            return new ProxyRequestQueryTransform {
                Id = Id,
                ConfigRevision = revision,
                RouteId = RouteId,
                RouteConfigRevision = revision,
                QueryOperation = RequestQueryTransformOperation.FromString(Operation) ?? throw new NotImplementedException($"Unknown Query Transform Operation: {Operation}"),
                QueryKey = QueryKey,
                QueryValue = QueryValue
            };
        }
    }

    public record RequestQueryTransformOperation(string Operation) {
        public static class Constants {
            public const string Set = "Set";
            public const string Append = "Append";
            public const string Remove = "Remove";
            public const string RouteSet = "RouteSet";
            public const string RouteAppend = "RouteAppend";
        }

        public static readonly RequestQueryTransformOperation Set = new(Constants.Set);
        public static readonly RequestQueryTransformOperation Append = new(Constants.Append);
        public static readonly RequestQueryTransformOperation Remove = new(Constants.Remove);
        public static readonly RequestQueryTransformOperation RouteSet = new(Constants.RouteSet);
        public static readonly RequestQueryTransformOperation RouteAppend = new(Constants.RouteAppend);

        public static ImmutableArray<RequestQueryTransformOperation> QueryTransformOperations = [Set, Append, Remove, RouteSet, RouteAppend];

        public static RequestQueryTransformOperation? FromString(string? value) => value switch {
            Constants.Set => Set,
            Constants.Append => Append,
            Constants.Remove => Remove,
            Constants.RouteSet => RouteSet,
            Constants.RouteAppend => RouteAppend,
            _ => null
        };

        public class QueryTransformOperationConverter : ValueConverter<RequestQueryTransformOperation?, string?> {
            public QueryTransformOperationConverter() : base(
                QueryTransformOperation => QueryTransformOperation == null ? null : QueryTransformOperation.Operation,
                    value => FromString(value)) { }
        }
    }
}
