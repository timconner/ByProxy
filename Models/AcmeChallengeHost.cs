namespace ByProxy.Models {
    public abstract class AcmeCertHost {
        public const string Discriminator = "Type";

        public Guid CertificateId { get; set; }
        public AcmeCert Certificate { get; set; }

        public string ChallengeType { get; init; }
        
        public string Host { get; set; }

        public AcmeCertHost() { }
        public AcmeCertHost(string challengeType) { ChallengeType = challengeType; }
    }

    public class AcmeHttpCertHost : AcmeCertHost {
        public AcmeHttpCertHost() : base(AcmeChallengeType.HTTP_01) { }
    }

    public class AcmeDnsCertHost : AcmeCertHost {
        public Guid DnsProviderId { get; set; }
        public AcmeDnsProvider DnsProvider { get; set; }

        public AcmeDnsCertHost() : base(AcmeChallengeType.DNS_01) { }
    }
}
