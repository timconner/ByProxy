namespace ByProxy.Models {
    public class ProxyRequestPathTransform : ProxyRequestTransform {
        public override RequestTransformType TransformType => RequestTransformType.Path;

        public RequestPathTransformMode PathMode { get; set; }
        public string PathString { get; set; }

        public override ProxyRequestTransform Clone(int newRevision) {
            return new ProxyRequestPathTransform {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                PathMode = PathMode,
                PathString = PathString
            };
        }

        public override RequestTransformExport ToExport() {
            return new RequestPathTransformExport {
                Id = Id,
                RouteId = RouteId,
                Mode = PathMode.Mode,
                PathString = PathString
            };
        }
    }

    public class RequestPathTransformExport : RequestTransformExport {
        public string Mode { get; set; }
        public string PathString { get; set; }

        public override ProxyRequestTransform Realize(int revision) {
            return new ProxyRequestPathTransform {
                Id = Id,
                ConfigRevision = revision,
                RouteId = RouteId,
                RouteConfigRevision = revision,
                PathMode = RequestPathTransformMode.FromString(Mode) ?? throw new NotImplementedException($"Unknown Path Transform Mode: {Mode}"),
                PathString = PathString
            };
        }
    }

    public record RequestPathTransformMode(string Mode) {
        public static class Constants {
            public const string Add = "Add";
            public const string Remove = "Remove";
            public const string Static = "Static";
            public const string Pattern = "Pattern";
        }

        public static readonly RequestPathTransformMode Add = new(Constants.Add);
        public static readonly RequestPathTransformMode Remove = new(Constants.Remove);
        public static readonly RequestPathTransformMode Static = new(Constants.Static);
        public static readonly RequestPathTransformMode Pattern = new(Constants.Pattern);

        public static ImmutableArray<RequestPathTransformMode> PathTransformModes = [Add, Remove, Static, Pattern];

        public static RequestPathTransformMode? FromString(string? value) => value switch {
            Constants.Add => Add,
            Constants.Remove => Remove,
            Constants.Static => Static,
            Constants.Pattern => Pattern,
            _ => null
        };

        public class PathTransformModeConverter : ValueConverter<RequestPathTransformMode?, string?> {
            public PathTransformModeConverter() : base(
                pathTransformMode => pathTransformMode == null ? null : pathTransformMode.Mode,
                    value => FromString(value)) { }
        }
    }
}
