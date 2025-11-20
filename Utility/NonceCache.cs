using System.Collections.Concurrent;

namespace ByProxy.Utility {
    public class NonceCache<TKey, TValue> 
        where TKey : notnull
        where TValue : notnull
    {
        private readonly object __lock = new();
        private readonly Dictionary<TKey, Queue<TValue>> _cache = new();

        public TValue? GetValueOrDefault(TKey key) {
            lock (__lock) {
                if (_cache.TryGetValue(key, out var queue)) {
                    if (queue.TryDequeue(out var nonce)) {
                        return nonce;
                    }
                }
            }
            return default;
        }

        public void Add(TKey key, TValue value) {
            lock (__lock) {
                if (_cache.TryGetValue(key, out var queue)) {
                    queue.Enqueue(value);
                } else {
                    var newQueue = new Queue<TValue>();
                    newQueue.Enqueue(value);
                    _cache.Add(key, newQueue);
                }
            }
        }
    }
}
