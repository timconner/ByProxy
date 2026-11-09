namespace ByProxy.Models {
    public abstract class ProxyResponseTransform {
        public Guid Id { get; set; }

        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public Guid RouteId { get; set; }
        public int RouteConfigRevision { get; set; }
        public ProxyRoute Route { get; set; }

        [NotMapped]
        public abstract ResponseTransformType TransformType { get; }

        public abstract ProxyResponseTransform Clone(int newRevision);

        public abstract ResponseTransformExport ToExport();
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ResponseHeaderTransformExport), "Header")]

    public abstract class ResponseTransformExport {
        public Guid Id { get; set; }
        public Guid RouteId { get; set; }

        public abstract ProxyResponseTransform Realize(int revision);
    }

    public record ResponseTransformType(string Type) {
        private const string _header = "Header";

        public static class Constants {
            public const string Discriminator = "Type";
        }

        public static readonly ResponseTransformType Header = new(_header);

        public static ResponseTransformType? FromString(string? value) => value switch {
            _header => Header,
            _ => null
        };

        public override string ToString() {
            return Type;
        }

        public class ResponseTransformTypeConverter : ValueConverter<ResponseTransformType?, string?> {
            public ResponseTransformTypeConverter() : base(
                routeTransformType => routeTransformType == null ? null : routeTransformType.Type,
                    value => FromString(value)) { }
        }
    }
}
