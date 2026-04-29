namespace ResxGenerator.VSExtension.Infrastructure
{
    public static class DictionaryExtensions
    {
        public static V? GetValueOrDefault<K, V>(this Dictionary<K, V> dict, K key)
        {
            return dict.TryGetValue(key, out var value)
                ? value
                : default;
        }
    }
}
