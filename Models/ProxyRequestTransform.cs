namespace ByProxy.Models {
    public abstract class ProxyRequestTransform {
        public Guid Id { get; set; }

        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public Guid RouteId { get; set; }
        public int RouteConfigRevision { get; set; }
        public ProxyRoute Route { get; set; }

        [NotMapped]
        public abstract RequestTransformType TransformType { get; }

        public abstract ProxyRequestTransform Clone(int newRevision);

        public abstract RequestTransformExport ToExport();
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(RequestPathTransformExport), "Path")]
    [JsonDerivedType(typeof(RequestMethodTransformExport), "Method")]
    [JsonDerivedType(typeof(RequestHeaderTransformExport), "Header")]
    [JsonDerivedType(typeof(RequestQueryTransformExport), "Query")]

    public abstract class RequestTransformExport {
        public Guid Id { get; set; }
        public Guid RouteId { get; set; }

        public abstract ProxyRequestTransform Realize(int revision);
    }

    public record RequestTransformType(string Type) {
        private const string _path = "Path";
        private const string _method = "Method";
        private const string _header = "Header";
        private const string _query = "Query";

        public static class Constants {
            public const string Discriminator = "Type";
        }

        public static readonly RequestTransformType Path = new(_path);
        public static readonly RequestTransformType Method = new(_method);
        public static readonly RequestTransformType Header = new(_header);
        public static readonly RequestTransformType Query = new(_query);

        public static RequestTransformType? FromString(string? value) => value switch {
            _path => Path,
            _method => Method,
            _header => Header,
            _query => Query,
            _ => null
        };

        public override string ToString() {
            return Type;
        }

        public class RequestTransformTypeConverter : ValueConverter<RequestTransformType?, string?> {
            public RequestTransformTypeConverter() : base(
                routeTransformType => routeTransformType == null ? null : routeTransformType.Type,
                    value => FromString(value)) { }
        }
    }
}
