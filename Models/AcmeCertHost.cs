namespace ByProxy.Models {
    public abstract class AcmeCertHost {
        public const string Discriminator = "Type";

        public Guid CertificateId { get; set; }
        public AcmeCert Certificate { get; set; }

        public string ChallengeType { get; init; }
        
        public string Host { get; set; }

        public AcmeCertHost() { }
        public AcmeCertHost(string challengeType) { ChallengeType = challengeType; }

        public abstract AcmeCertHostExport ToExport();
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(AcmeHttpCertHostExport), AcmeChallengeType.HTTP_01)]
    [JsonDerivedType(typeof(AcmeDnsCertHostExport), AcmeChallengeType.DNS_01)]
    public abstract class AcmeCertHostExport {
        public string Host { get; set; }
    }

    public class AcmeHttpCertHost : AcmeCertHost {
        public AcmeHttpCertHost() : base(AcmeChallengeType.HTTP_01) { }
        public override AcmeCertHostExport ToExport() {
            return new AcmeHttpCertHostExport {
                Host = Host
            };
        }
    }

    public class AcmeHttpCertHostExport : AcmeCertHostExport { }

    public class AcmeDnsCertHost : AcmeCertHost {
        public Guid DnsProviderId { get; set; }
        public AcmeDnsProvider DnsProvider { get; set; }

        public AcmeDnsCertHost() : base(AcmeChallengeType.DNS_01) { }

        public override AcmeCertHostExport ToExport() {
            return new AcmeDnsCertHostExport {
                Host = Host,
                DnsProviderId = DnsProviderId
            };
        }
    }

    public class AcmeDnsCertHostExport : AcmeCertHostExport {
        public Guid DnsProviderId { get; set; }
    }
}
