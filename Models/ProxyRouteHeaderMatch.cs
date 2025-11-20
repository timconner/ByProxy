namespace ByProxy.Models {
    public class ProxyRouteHeaderMatch : ProxyRouteMatch {
        public override RouteMatchType MatchType => RouteMatchType.Header;

        public ProxyHeaderMatchMode HeaderMatchMode { get; set; }

        public override ProxyRouteMatch Clone(int newRevision) {
            return new ProxyRouteHeaderMatch() {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                HeaderMatchMode = HeaderMatchMode,
                Key = Key,
                Values = Values,
                CaseSensitiveValues = CaseSensitiveValues
            };
        }

        public RouteHeader ToYarp() {
            return new RouteHeader {
                Mode = HeaderMatchMode.Enum,
                Name = Key,
                Values = Values,
                IsCaseSensitive = CaseSensitiveValues ?? false
            };
        }

        public override dynamic ToComparable() {
            return new {
                Id = Id,
                Type = MatchType.Type,
                MatchMode = HeaderMatchMode.Mode,
                Header = Key,
                Values = Values,
                CaseSensitiveValues = CaseSensitiveValues
            };
        }
    }

    public record ProxyHeaderMatchMode(string Mode, HeaderMatchMode Enum, bool HasValues) {
        public static class Constants {
            public const string Exact = "Exact";
            public const string Prefix = "Prefix";
            public const string Contains = "Contains";
            public const string NotContains = "NotContains";
            public const string Exists = "Exists";
            public const string NotExists = "NotExists";
        }

        public static readonly ProxyHeaderMatchMode Exact = new(Constants.Exact, HeaderMatchMode.ExactHeader, true);
        public static readonly ProxyHeaderMatchMode Prefix = new(Constants.Prefix, HeaderMatchMode.HeaderPrefix, true);
        public static readonly ProxyHeaderMatchMode Contains = new(Constants.Contains, HeaderMatchMode.Contains, true);
        public static readonly ProxyHeaderMatchMode NotContains = new(Constants.NotContains, HeaderMatchMode.NotContains, true);
        public static readonly ProxyHeaderMatchMode Exists = new(Constants.Exists, HeaderMatchMode.Exists, false);
        public static readonly ProxyHeaderMatchMode NotExists = new(Constants.NotExists, HeaderMatchMode.NotExists, false);

        public static ImmutableArray<ProxyHeaderMatchMode> HeaderMatchModes = [Exact, Prefix, Contains, NotContains, Exists, NotExists];

        public static ProxyHeaderMatchMode? FromString(string? value) => value switch {
            Constants.Exact => Exact,
            Constants.Prefix => Prefix,
            Constants.Contains => Contains,
            Constants.NotContains => NotContains,
            Constants.Exists => Exists,
            Constants.NotExists => NotExists,
            _ => null
        };

        public class HeaderMatchModeConverter : ValueConverter<ProxyHeaderMatchMode?, string?> {
            public HeaderMatchModeConverter() : base(
                headerMatchMode => headerMatchMode == null ? null : headerMatchMode.Mode,
                    value => FromString(value)) { }
        }
    }
}
