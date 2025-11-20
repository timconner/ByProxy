namespace ByProxy.Models {
    public class ProxyRouteQueryTransform : ProxyRouteTransform {
        public override RouteTransformType TransformType => RouteTransformType.Query;

        public QueryTransformOperation QueryOperation { get; set; }
        public string QueryKey { get; set; }
        public string? QueryValue { get; set; }

        public override ProxyRouteTransform Clone(int newRevision) {
            return new ProxyRouteQueryTransform() {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                QueryOperation = QueryOperation,
                QueryKey = QueryKey,
                QueryValue = QueryValue
            };
        }

        public override dynamic ToComparable() {
            return new {
                Id = Id,
                Type = TransformType.Type,
                QueryOperation = QueryOperation.Operation,
                QueryKey = QueryKey,
                QueryValue = QueryValue
            };
        }
    }

    public record QueryTransformOperation(string Operation) {
        public static class Constants {
            public const string Set = "Set";
            public const string Append = "Append";
            public const string Remove = "Remove";
            public const string RouteSet = "RouteSet";
            public const string RouteAppend = "RouteAppend";
        }

        public static readonly QueryTransformOperation Set = new(Constants.Set);
        public static readonly QueryTransformOperation Append = new(Constants.Append);
        public static readonly QueryTransformOperation Remove = new(Constants.Remove);
        public static readonly QueryTransformOperation RouteSet = new(Constants.RouteSet);
        public static readonly QueryTransformOperation RouteAppend = new(Constants.RouteAppend);

        public static ImmutableArray<QueryTransformOperation> QueryTransformOperations = [Set, Append, Remove, RouteSet, RouteAppend];

        public static QueryTransformOperation? FromString(string? value) => value switch {
            Constants.Set => Set,
            Constants.Append => Append,
            Constants.Remove => Remove,
            Constants.RouteSet => RouteSet,
            Constants.RouteAppend => RouteAppend,
            _ => null
        };

        public class QueryTransformOperationConverter : ValueConverter<QueryTransformOperation?, string?> {
            public QueryTransformOperationConverter() : base(
                QueryTransformOperation => QueryTransformOperation == null ? null : QueryTransformOperation.Operation,
                    value => FromString(value)) { }
        }
    }
}
