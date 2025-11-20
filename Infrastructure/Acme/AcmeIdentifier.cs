namespace ByProxy.Infrastructure.Acme {
    public struct AcmeIdentifier {
        public string Type { get; init; }
        public string Value { get; init; }

        public AcmeIdentifier(string type, string value) {
            Type = type;
            Value = value;
        }
    }
}
