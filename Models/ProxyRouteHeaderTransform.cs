namespace ByProxy.Models {
    public class ProxyRouteHeaderTransform : ProxyRouteTransform {
        public override RouteTransformType TransformType => RouteTransformType.Header;

        public HeaderTransformOperation HeaderOperation { get; set; }
        public string HeaderName { get; set; }
        public string? HeaderValue { get; set; }

        public override ProxyRouteTransform Clone(int newRevision) {
            return new ProxyRouteHeaderTransform() {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                HeaderOperation = HeaderOperation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }

        public override dynamic ToComparable() {
            return new {
                Id = Id,
                Type = TransformType.Type,
                HeaderOperation = HeaderOperation.Operation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }
    }

    public record HeaderTransformOperation(string Operation) {
        public static class Constants {
            public const string Set = "Set";
            public const string Append = "Append";
            public const string Remove = "Remove";
            public const string RouteSet = "RouteSet";
            public const string RouteAppend = "RouteAppend";
        }

        public static readonly HeaderTransformOperation Set = new(Constants.Set);
        public static readonly HeaderTransformOperation Append = new(Constants.Append);
        public static readonly HeaderTransformOperation Remove = new(Constants.Remove);
        public static readonly HeaderTransformOperation RouteSet = new(Constants.RouteSet);
        public static readonly HeaderTransformOperation RouteAppend = new(Constants.RouteAppend);

        public static ImmutableArray<HeaderTransformOperation> HeaderTransformOperations = [Set, Append, Remove, RouteSet, RouteAppend];

        public static HeaderTransformOperation? FromString(string? value) => value switch {
            Constants.Set => Set,
            Constants.Append => Append,
            Constants.Remove => Remove,
            Constants.RouteSet => RouteSet,
            Constants.RouteAppend => RouteAppend,
            _ => null
        };

        public class HeaderTransformOperationConverter : ValueConverter<HeaderTransformOperation?, string?> {
            public HeaderTransformOperationConverter() : base(
                headerTransformOperation => headerTransformOperation == null ? null : headerTransformOperation.Operation,
                    value => FromString(value)) { }
        }
    }
}
