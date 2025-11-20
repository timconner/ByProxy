namespace ByProxy.Models {
    public abstract class ProxyRouteMatch {
        public Guid Id { get; set; }

        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public Guid RouteId { get; set; }
        public int RouteConfigRevision { get; set; }
        public ProxyRoute Route { get; set; }

        public string Key { get; set; }
        public List<string>? Values { get; set; }
        public bool? CaseSensitiveValues { get; set; }

        [NotMapped]
        public abstract RouteMatchType MatchType { get; }

        public abstract ProxyRouteMatch Clone(int newRevision);

        public abstract dynamic ToComparable();
    }

    public static class ProxyRouteMatchExtensions {
        public static List<RouteHeader>? ToYarpHeaders(this IEnumerable<ProxyRouteMatch> matchCriteria) {
            var headerMatches = matchCriteria.OfType<ProxyRouteHeaderMatch>();
            if (headerMatches.Any()) return headerMatches.Select(_ => _.ToYarp()).ToList();
            return null;
        }

        public static List<RouteQueryParameter>? ToYarpQueryParameters(this IEnumerable<ProxyRouteMatch> matchCriteria) {
            var queryMatches = matchCriteria.OfType<ProxyRouteQueryMatch>();
            if (queryMatches.Any()) return queryMatches.Select(_ => _.ToYarp()).ToList();
            return null;
        }
    }

    public record RouteMatchType(string Type) {
        private const string _header = "Header";
        private const string _query = "Query";

        public static class Constants {
            public const string Discriminator = "Type";
        }

        public static readonly RouteMatchType Header = new(_header);
        public static readonly RouteMatchType Query = new(_query);

        public static RouteMatchType? FromString(string? value) => value switch {
            _header => Header,
            _query => Query,
            _ => null
        };

        public override string ToString() {
            return Type;
        }

        public class RouteMatchTypeConverter : ValueConverter<RouteMatchType?, string?> {
            public RouteMatchTypeConverter() : base(
                routeMatchType => routeMatchType == null ? null : routeMatchType.Type,
                    value => FromString(value)) { }
        }
    }
}
