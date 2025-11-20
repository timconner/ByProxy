namespace ByProxy.Models {
    public class AcmeAccount {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }
        public string Url { get; set; }
        public bool Hidden { get; set; }
    }
}
