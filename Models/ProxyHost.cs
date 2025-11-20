namespace ByProxy.Models
{
    public class ProxyHost : IVersioned {
        public Guid Id { get; set; }

        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public string Host { get; set; }
    }
}
