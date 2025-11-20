namespace ByProxy.Utility {
    public sealed class HostTrie {
        private sealed class TrieBuilderNode {
            public readonly Dictionary<string, TrieBuilderNode> Children = new(StringComparer.OrdinalIgnoreCase);
            public Guid? Id;
        }

        private sealed class TrieNode {
            public readonly ImmutableDictionary<string, TrieNode> Children;
            public readonly Guid? Id;
            public TrieNode(TrieBuilderNode builderNode) {
                Id = builderNode.Id;
                
                var children = ImmutableDictionary.CreateBuilder<string, TrieNode>(StringComparer.OrdinalIgnoreCase);
                foreach (var child in builderNode.Children) {
                    children.Add(child.Key, new TrieNode(child.Value));
                }
                Children = children.ToImmutable();
            }
        }

        private readonly TrieNode _rootNode;
        public HostTrie(List<ProxySniMap> hostMaps) {
            var rootNode = new TrieBuilderNode();
            foreach (var map in hostMaps) {
                var hostParts = SplitHostname(map.Host);
                var curNode = rootNode;
                foreach (var hostPart in hostParts) {
                    if (!curNode.Children.TryGetValue(hostPart, out var nextNode)) {
                        nextNode = new TrieBuilderNode();
                        curNode.Children.Add(hostPart, nextNode);
                    }
                    curNode = nextNode;
                }
                curNode.Id = map.CertificateId;
            }
            _rootNode = new TrieNode(rootNode);
        }

        private readonly IdnMapping _idn = new();
        private string[] SplitHostname(string hostname) {
            var hostParts = hostname.Split('.');
            Array.Reverse(hostParts);
            for (int i = 0; i < hostParts.Length; i++) {
                if (hostParts[i] == "*") continue;
                hostParts[i] = _idn.GetAscii(hostParts[i]);
            }
            return hostParts;
        }

        public bool TryGetCertIdByHost(string hostname, out Guid certId) {
            certId = Guid.Empty;
            var hostParts = SplitHostname(hostname);

            int i;
            var curNode = _rootNode;
            for (i = 0; i < hostParts.Length - 1; i++) {
                if (curNode.Children.TryGetValue(hostParts[i], out var nextNode)) {
                    curNode = nextNode;
                    continue;
                }
                return false;
            }

            if (curNode.Children.TryGetValue(hostParts[i], out var exactNode) && exactNode.Id != null) {
                certId = exactNode.Id.Value;
                return true;
            }
            if (curNode.Children.TryGetValue("*", out var wildcardNode) && wildcardNode.Id != null) {
                certId = wildcardNode.Id.Value;
                return true;
            }

            return false;
        }
    }
}
