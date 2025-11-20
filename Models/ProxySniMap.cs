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

        public dynamic ToComparable() {
            return new {
                Host = Host,
                CertId = CertificateId
            };
        }
    }
}
