namespace ByProxy.Data {
    public class DictionaryConverter<TKey, TValue> : ValueConverter<Dictionary<TKey, TValue>?, string?> where TKey : notnull {
        public DictionaryConverter()
            : base(
                dictionary => JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
                json => json == null ? null : JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(json)) { }
    }

    public class DictionaryComparer<TKey, TValue> : ValueComparer<Dictionary<TKey, TValue>?> where TKey : notnull {
        public DictionaryComparer()
            : base(
                (a, b) => AreDictionariesEqual(a, b),
                dictionary => GetDictionaryHashCode(dictionary),
                dictionary => CopyDictionary(dictionary)) { }

        private static bool AreDictionariesEqual(Dictionary<TKey, TValue>? a, Dictionary<TKey, TValue>? b) {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            return !a.Except(b).Any();
        }

        private static int GetDictionaryHashCode(Dictionary<TKey, TValue>? dictionary) {
            if (dictionary == null) return 0;

            int hashCode = 0;
            foreach (var kvp in dictionary.OrderBy(_ => _.Key)) {
                hashCode = HashCode.Combine(hashCode, kvp.Key, kvp.Value);
            }

            return hashCode;
        }

        private static Dictionary<TKey, TValue>? CopyDictionary(Dictionary<TKey, TValue>? dictionary) {
            if (dictionary == null) return null;
            return new Dictionary<TKey, TValue>(dictionary, dictionary.Comparer);
        }
    }
}
