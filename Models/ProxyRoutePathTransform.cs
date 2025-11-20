namespace ByProxy.Models {
    public class ProxyRoutePathTransform : ProxyRouteTransform {
        public override RouteTransformType TransformType => RouteTransformType.Path;

        public PathTransformMode PathMode { get; set; }
        public string PathString { get; set; }

        public override ProxyRouteTransform Clone(int newRevision) {
            return new ProxyRoutePathTransform() {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                PathMode = PathMode,
                PathString = PathString
            };
        }

        public override dynamic ToComparable() {
            return new {
                Id = Id,
                Type = TransformType.Type,
                PathMode = PathMode.Mode,
                PathString = PathString
            };
        }
    }

    public record PathTransformMode(string Mode) {
        public static class Constants {
            public const string Add = "Add";
            public const string Remove = "Remove";
            public const string Static = "Static";
            public const string Pattern = "Pattern";
        }

        public static readonly PathTransformMode Add = new(Constants.Add);
        public static readonly PathTransformMode Remove = new(Constants.Remove);
        public static readonly PathTransformMode Static = new(Constants.Static);
        public static readonly PathTransformMode Pattern = new(Constants.Pattern);

        public static ImmutableArray<PathTransformMode> PathTransformModes = [Add, Remove, Static, Pattern];

        public static PathTransformMode? FromString(string? value) => value switch {
            Constants.Add => Add,
            Constants.Remove => Remove,
            Constants.Static => Static,
            Constants.Pattern => Pattern,
            _ => null
        };

        public class PathTransformModeConverter : ValueConverter<PathTransformMode?, string?> {
            public PathTransformModeConverter() : base(
                pathTransformMode => pathTransformMode == null ? null : pathTransformMode.Mode,
                    value => FromString(value)) { }
        }
    }
}
