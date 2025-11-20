namespace ByProxy.Models {
    public class ProxyRouteQueryMatch : ProxyRouteMatch {
        public override RouteMatchType MatchType => RouteMatchType.Query;

        public ProxyQueryMatchMode QueryMatchMode { get; set; }

        public override ProxyRouteMatch Clone(int newRevision) {
            return new ProxyRouteQueryMatch() {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                QueryMatchMode = QueryMatchMode,
                Key = Key,
                Values = Values,
                CaseSensitiveValues = CaseSensitiveValues
            };
        }

        public RouteQueryParameter ToYarp() {
            return new RouteQueryParameter {
                Mode = QueryMatchMode.Enum,
                Name = Key,
                Values = Values,
                IsCaseSensitive = CaseSensitiveValues ?? false
            };
        }

        public override dynamic ToComparable() {
            return new {
                Id = Id,
                Type = MatchType.Type,
                MatchMode = QueryMatchMode.Mode,
                Parameter = Key,
                Values = Values,
                CaseSensitiveValues = CaseSensitiveValues
            };
        }
    }

    public record ProxyQueryMatchMode(string Mode, QueryParameterMatchMode Enum, bool HasValues) {
        public static class Constants {
            public const string Exact = "Exact";
            public const string Prefix = "Prefix";
            public const string Contains = "Contains";
            public const string NotContains = "NotContains";
            public const string Exists = "Exists";
        }

        public static readonly ProxyQueryMatchMode Exact = new(Constants.Exact, QueryParameterMatchMode.Exact, true);
        public static readonly ProxyQueryMatchMode Prefix = new(Constants.Prefix, QueryParameterMatchMode.Prefix, true);
        public static readonly ProxyQueryMatchMode Contains = new(Constants.Contains, QueryParameterMatchMode.Contains, true);
        public static readonly ProxyQueryMatchMode NotContains = new(Constants.NotContains, QueryParameterMatchMode.NotContains, true);
        public static readonly ProxyQueryMatchMode Exists = new(Constants.Exists, QueryParameterMatchMode.Exists, false);

        public static ImmutableArray<ProxyQueryMatchMode> QueryMatchModes = [Exact, Prefix, Contains, NotContains, Exists];

        public static ProxyQueryMatchMode? FromString(string? value) => value switch {
            Constants.Exact => Exact,
            Constants.Prefix => Prefix,
            Constants.Contains => Contains,
            Constants.NotContains => NotContains,
            Constants.Exists => Exists,
            _ => null
        };

        public class QueryMatchModeConverter : ValueConverter<ProxyQueryMatchMode?, string?> {
            public QueryMatchModeConverter() : base(
                queryMatchMode => queryMatchMode == null ? null : queryMatchMode.Mode,
                    value => FromString(value)) { }
        }
    }
}
