namespace ByProxy.Infrastructure.Proxy {
    public sealed class RunningIpRestriction {
        public required bool IsAllowList { get; init; }
        public required ImmutableArray<RunningIpRange> Ranges { get; init; }
        public required RunningStaticResponse DenyResponse { get; init; }

        public bool IsAllowed(IPAddress? address) {
            if (address == null) return false;
            
            byte[] bytes;
            if (address.IsIPv4MappedToIPv6) {
                bytes = address.MapToIPv4().GetAddressBytes();
            } else {
                bytes = address.GetAddressBytes();
            }

            foreach (var range in Ranges) {
                if (range.Contains(bytes)) return IsAllowList;
            }
            return !IsAllowList;
        }
    }
}
