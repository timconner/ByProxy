namespace ByProxy.Models {
    public class ProxyCluster : IVersioned {
        public Guid Id { get; set; }
        
        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public List<ProxyDestination> Destinations { get; set; }

        public LoadBalancingPolicy? LoadBalancing { get; set; }
        public bool StickySessions { get; set; }

        public bool AcceptAnyCert { get; set; }

        public static readonly ClusterConfig NullCluster = new ClusterConfig {
            ClusterId = "Null",
            Destinations = new Dictionary<string, DestinationConfig> {
                    { "Null", new DestinationConfig() { Address = "http://127.0.0.1:0" } } }
        };

        public List<string> GetValidationErrors() {
            var errors = new List<string>();
            if (Destinations.Count == 0) errors.Add("ERR_DestinationsEmpty");
            if (string.IsNullOrWhiteSpace(Name)) errors.Add("ERR_NameEmpty");
            return errors;
        }

        public ClusterConfig ToYarp() {
            var destinations = Destinations
                .ToStringSortableDictionary()
                .ToDictionary(_ => _.Key, _ => _.Value.ToYarp());

            SessionAffinityConfig? sessionAffinity = null;
            if (StickySessions) {
                sessionAffinity = new SessionAffinityConfig {
                    Enabled = true,
                    Policy = "HashCookie",
                    AffinityKeyName = $"Affinity_{Id}",
                };
            }

            return new ClusterConfig() {
                ClusterId = Id.ToString(),
                Destinations = destinations,
                LoadBalancingPolicy = LoadBalancing?.Policy,
                SessionAffinity = sessionAffinity,
                HttpClient = new HttpClientConfig() {
                    DangerousAcceptAnyServerCertificate = AcceptAnyCert
                }
            };
        }

        public ProxyCluster Clone(int newRevision) {
            return new ProxyCluster() {
                Id = Id,
                ConfigRevision = newRevision,
                Name = Name,
                Description = Description,
                Destinations = Destinations.Select(_ => _.Clone(newRevision)).ToList(),
                LoadBalancing = LoadBalancing,
                StickySessions = StickySessions,
                AcceptAnyCert = AcceptAnyCert
            };
        }

        public dynamic ToComparable(bool sortByOrder) {
            return new {
                Id = Id,
                Name = Name,
                Description = Description,
                Destinations = Destinations
                    .OrderBy(_ => sortByOrder ? (IComparable)_.Order : (IComparable)_.Id)
                    .Select(_ => _.ToComparable())
                    .ToArray(),
                LoadBalancing = LoadBalancing,
                StickySessions = StickySessions,
                AcceptAnyCert = AcceptAnyCert
            };
        }
    }

    public record LoadBalancingPolicy(string Policy) {
        private const string _powerOfTwoChoices = "PowerOfTwoChoices";
        private const string _firstAlphabetical = "FirstAlphabetical";
        private const string _random = "Random";
        private const string _roundRobin = "RoundRobin";
        private const string _leastRequests = "LeastRequests";

        public static readonly LoadBalancingPolicy PowerOfTwoChoices = new(_powerOfTwoChoices);
        public static readonly LoadBalancingPolicy FirstAlphabetical = new(_firstAlphabetical);
        public static readonly LoadBalancingPolicy Random = new(_random);
        public static readonly LoadBalancingPolicy RoundRobin = new(_roundRobin);
        public static readonly LoadBalancingPolicy LeastRequests = new(_leastRequests);

        public static readonly ImmutableArray<LoadBalancingPolicy> AllPolicies = [
            PowerOfTwoChoices, FirstAlphabetical, Random, RoundRobin, LeastRequests
        ];

        public static LoadBalancingPolicy? FromString(string? value) => value switch {
            _powerOfTwoChoices => PowerOfTwoChoices,
            _firstAlphabetical => FirstAlphabetical,
            _random => Random,
            _roundRobin => RoundRobin,
            _leastRequests => LeastRequests,
            _ => null
        };

        public class LoadBalancingPolicyConverter : ValueConverter<LoadBalancingPolicy?, string?> {
            public LoadBalancingPolicyConverter() : base(
                policy => policy == null ? null : policy.Policy,
                    value => FromString(value)) { }
        }
    }
}
