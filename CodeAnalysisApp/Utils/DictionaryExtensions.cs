using System.Collections.Generic;

namespace CodeAnalysisApp.Utils
{
    public static class DictionaryExtensions
    {
        public static TValue GetOrDefault<TKey, TValue>(
            this IDictionary<TKey, object> dict,
            TKey key,
            TValue defaultValue = default) =>
            dict.TryGetValue(key, out var val) ? (TValue)val : defaultValue;

        public static TValue AddOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            where TValue : new()
        {
            if (!dictionary.TryGetValue(key, out var val))
            {
                val = new TValue();
                dictionary[key] = val;
            }

            return val;
        }

        public static bool GetOrFalse<TKey>(
            this IDictionary<TKey, object> dict,
            TKey key) =>
            dict.GetOrDefault(key, false);
    }
}
