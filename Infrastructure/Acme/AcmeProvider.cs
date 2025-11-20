namespace ByProxy.Infrastructure.Acme {
    public class AcmeProvider {
        public string Id { get; init; }
        public string Name { get; init; }
        public string DirectoryUrl { get; init; }
        public string[] SupportedChallenges { get; init; }
        public bool ContactEmailsOptional { get; init; }

        public AcmeProvider(string id, string name, string directoryUrl, string? stagingUrl, string[] supportedChallenges, bool contactEmailsOptional) {
            Id = id;
            Name = name;
            DirectoryUrl = directoryUrl;
            SupportedChallenges = supportedChallenges;
            ContactEmailsOptional = contactEmailsOptional;
        }
    }
}
