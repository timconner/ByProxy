namespace ByProxy.Models {
    public class CertRequest {
        public string Name { get; set; }
        public List<string> Hosts { get; set; }

        public CertRequest() {
            Name = string.Empty;
            Hosts = new List<string>();
        }
    }
}
