using System.Collections.Concurrent;

namespace ByProxy.Utility {
    public class ExpiringObjectCache<TKey, TValue> where TKey : notnull {
        private readonly object __lock = new();
        private readonly Dictionary<TKey, ExpiringObject<TValue>> _cache = new();

        public TValue? GetValueOrDefault(TKey key) {
            IDisposable? toDispose = null;
            lock (__lock) {
                var cachedObject = _cache.GetValueOrDefault(key);
                if (cachedObject != null) {
                    if (cachedObject.Expiry >= DateTime.UtcNow) return cachedObject.Value;
                    _cache.Remove(key);
                    if (cachedObject is IDisposable disposable) toDispose = disposable;
                }
            }
            toDispose?.Dispose();
            return default;
        }

        public void AddOrUpdate(TKey key, TValue value, TimeSpan objectTimeToLive) {
            IDisposable? toDispose = null;
            lock (__lock) {
                if (_cache.TryGetValue(key, out var old)) {
                    if (old is IDisposable disposable) toDispose = disposable;
                }
                var newObject = new ExpiringObject<TValue>(value, DateTime.UtcNow.Add(objectTimeToLive));
                _cache[key] = newObject;
            }
            toDispose?.Dispose();
        }

        private class ExpiringObject<T> {
            public DateTime Expiry { get; init; }
            public T Value { get; init; }

            public ExpiringObject(T value, DateTime expiresAt) {
                Expiry = expiresAt;
                Value = value;
            }
        }
    }
}
