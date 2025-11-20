namespace ByProxy.Models {
    public abstract class ProxyRouteTransform {
        public Guid Id { get; set; }

        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public Guid RouteId { get; set; }
        public int RouteConfigRevision { get; set; }
        public ProxyRoute Route { get; set; }

        [NotMapped]
        public abstract RouteTransformType TransformType { get; }

        public abstract ProxyRouteTransform Clone(int newRevision);

        public abstract dynamic ToComparable();
    }

    public record RouteTransformType(string Type) {
        private const string _path = "Path";
        private const string _method = "Method";
        private const string _header = "Header";
        private const string _query = "Query";

        public static class Constants {
            public const string Discriminator = "Type";
        }

        public static readonly RouteTransformType Path = new(_path);
        public static readonly RouteTransformType Method = new(_method);
        public static readonly RouteTransformType Header = new(_header);
        public static readonly RouteTransformType Query = new(_query);

        public static RouteTransformType? FromString(string? value) => value switch {
            _path => Path,
            _method => Method,
            _header => Header,
            _query => Query,
            _ => null
        };

        public override string ToString() {
            return Type;
        }

        public class RouteTransformTypeConverter : ValueConverter<RouteTransformType?, string?> {
            public RouteTransformTypeConverter() : base(
                routeTransformType => routeTransformType == null ? null : routeTransformType.Type,
                    value => FromString(value)) { }
        }
    }
}
