namespace ByProxy.Models {
    public interface IVersioned {
        public ProxyConfig Config { get; }
        public int ConfigRevision { get; }
    }
}
