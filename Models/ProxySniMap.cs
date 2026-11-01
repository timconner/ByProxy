namespace ByProxy.Models {
    public class ProxySniMap : IVersioned {
        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public string Host {  get; set; }

        public Guid CertificateId { get; set; }
        public ServerCert Certificate { get; set; }

        public ProxySniMap Clone(int newRevision) {
            return new ProxySniMap {
                ConfigRevision = newRevision,
                Host = Host,
                CertificateId = CertificateId
            };
        }

        public SniMapExport ToExport() {
            return new SniMapExport {
                Host = Host,
                CertificateId = CertificateId
            };
        }
    }

    public class SniMapExport {
        public string Host { get; set; }
        public Guid CertificateId { get; set; }

        public ProxySniMap Realize(int revision) {
            return new ProxySniMap {
                ConfigRevision = revision,
                Host = Host,
                CertificateId = CertificateId
            };
        }
    }
}
