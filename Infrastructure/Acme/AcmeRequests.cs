namespace ByProxy.Infrastructure.Acme {
    public class AcmeNewAccountRequest {
        public bool TermsOfServiceAgreed => true;
        public string[]? Contact { get; init; }

        public AcmeNewAccountRequest(IEnumerable<string>? contactEmails) {
            if (contactEmails != null && contactEmails.Any()) {
                List<string> contacts = new();
                foreach (var email in contactEmails) {
                    contacts.Add($"mailto:{email}");
                }
                Contact = contacts.ToArray();
            }
        }
    }

    public class AcmeNewOrderRequest {
        public IEnumerable<AcmeIdentifier> Identifiers { get; init;}

        public AcmeNewOrderRequest(IEnumerable<AcmeIdentifier> identifiers) { Identifiers = identifiers; }

        public static AcmeNewOrderRequest CreateDnsOrder(IEnumerable<string> dnsNames) {
            var identifiers = new List<AcmeIdentifier>();
            foreach (var name in dnsNames) {
                identifiers.Add(new AcmeIdentifier("dns", name));
            }
            return new AcmeNewOrderRequest(identifiers);
        }
    }
}
