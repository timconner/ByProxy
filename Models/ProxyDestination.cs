namespace ByProxy.Models {
    public class ProxyDestination : IOrderable, IVersioned {
        public Guid Id { get; set; }

        public int ConfigRevision { get; set; }
        public ProxyConfig Config { get; set; }

        public string Name { get; set; }
        public Guid ClusterId { get; set; }
        public int ClusterConfigRevision { get; set; }
        public ProxyCluster Cluster { get; set; }

        public int Order { get; set; }
        public string Address { get; set; }
        public string? Health { get; set; }

        public DestinationConfig ToYarp() {
            return new DestinationConfig() { Address = Address, Health = Health };   
        }

        public ProxyDestination Clone(int newRevision) {
            return new ProxyDestination() {
                Id = Id,
                ConfigRevision = newRevision,
                Name = Name,
                ClusterId = ClusterId,
                ClusterConfigRevision = newRevision,
                Order = Order,
                Address = Address,
                Health = Health
            };
        }

        public dynamic ToComparable() {
            return new {
                Id = Id,
                Name = Name,
                Order = Order,
                Address = Address,
                Health = Health
            };
        }
    }
}
