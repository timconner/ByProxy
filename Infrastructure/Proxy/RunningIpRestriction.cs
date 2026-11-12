namespace ByProxy.Infrastructure.Proxy {
    public sealed class IpRangeParseException : FormatException {
        public string ErrorKey { get; }

        public IpRangeParseException(string errorKey, string message) : base(message) {
            ErrorKey = errorKey;
        }
    }

    public sealed class RunningIpRange {
        public required ReadOnlyMemory<byte> Start { get; init; }
        public required ReadOnlyMemory<byte> End { get; init; }

        public bool Contains(byte[] addressAsBytes) {           
            // Ensure Same Family i.e. Both IPv4 or both IPv6
            if (addressAsBytes.Length != Start.Length) return false;

            return addressAsBytes.AsSpan().SequenceCompareTo(Start.Span) >= 0
                && addressAsBytes.AsSpan().SequenceCompareTo(End.Span) <= 0;
        }

        private static bool TryParseAddress(string value, [NotNullWhen(true)] out IPAddress? address) {
            value = value.Trim();

            if (!IPAddress.TryParse(value, out address)) return false;

            //Ensure Parsed IP is Same as Typed IP
            return address.AddressFamily != AddressFamily.InterNetwork || value == address.ToString();
        }

        public static RunningIpRange Parse(string entry) {
            entry = entry.Trim();

            if (entry.Contains('/')) {
                var parts = entry.Split('/');

                if (parts.Length != 2 || !TryParseAddress(parts[0], out var address)) {
                    throw new IpRangeParseException("ERR_IpRestrictionInvalidCidr", $"Invalid CIDR: {entry}");
                }

                var bytes = address.GetAddressBytes();
                var maxPrefix = bytes.Length * 8;

                if (
                    !int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var prefix)
                    || prefix < 0
                    || prefix > maxPrefix
                ) {
                    throw new IpRangeParseException("ERR_IpRestrictionInvalidPrefix", $"Invalid CIDR prefix length: {entry}");
                }

                if (address.IsIPv4MappedToIPv6 && prefix < 96) {
                    throw new IpRangeParseException("ERR_IpRestrictionMappedPrefix", $"IPv4-mapped IPv6 CIDRs require a prefix length of at least 96: {entry}");
                }

                var start = new byte[bytes.Length];
                var end = new byte[bytes.Length];

                for (var i = 0; i < bytes.Length; i++) {
                    var prefixBits = Math.Clamp(prefix - i * 8, 0, 8);
                    var mask = (0xFF << (8 - prefixBits)) & 0xFF;
                    var hostMask = 0xFF ^ mask;

                    start[i] = (byte)(bytes[i] & mask);
                    end[i] = (byte)(start[i] | hostMask);
                }

                if (address.IsIPv4MappedToIPv6) {
                    return Create(new IPAddress(start), new IPAddress(end));
                }

                return new RunningIpRange {
                    Start = start,
                    End = end
                };
            }

            if (entry.Contains('-')) {
                var parts = entry.Split('-');

                if (parts.Length != 2 ||
                    !TryParseAddress(parts[0], out var start) ||
                    !TryParseAddress(parts[1], out var end)) {
                    throw new IpRangeParseException("ERR_IpRestrictionInvalidRange", $"Invalid IP range: {entry}");
                }

                return Create(start, end);
            }

            if (!TryParseAddress(entry, out var single)) {
                throw new IpRangeParseException("ERR_IpRestrictionInvalidAddress", $"Invalid IP address: {entry}");
            }

            return Create(single, single);
        }

        private static RunningIpRange Create(IPAddress start, IPAddress end) {
            if (start.IsIPv4MappedToIPv6) start = start.MapToIPv4();
            if (end.IsIPv4MappedToIPv6) end = end.MapToIPv4();

            var startBytes = start.GetAddressBytes();
            var endBytes = end.GetAddressBytes();

            if (startBytes.Length != endBytes.Length) {
                throw new IpRangeParseException("ERR_IpRestrictionRangeFamilyMismatch", $"IP range endpoints must use the same address family: {start}-{end}");
            }

            if (startBytes.AsSpan().SequenceCompareTo(endBytes) > 0) {
                throw new IpRangeParseException("ERR_IpRestrictionRangeOrder", $"IP range start exceeds its end: {start}-{end}");
            }

            return new RunningIpRange {
                Start = startBytes,
                End = endBytes
            };
        }
    }

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
