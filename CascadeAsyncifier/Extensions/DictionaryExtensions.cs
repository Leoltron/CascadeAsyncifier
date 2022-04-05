using System.Collections.Generic;

namespace CascadeAsyncifier.Extensions
{
    public static class DictionaryExtensions
    {
        public static TValue GetOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> dict,
            TKey key,
            TValue defaultValue = default) =>
            dict.TryGetValue(key, out var val) ? val : defaultValue;
        public static TValue GetOrDefault<TKey, TValue>(
            this IDictionary<TKey, object> dict,
            TKey key,
            TValue defaultValue = default) =>
            dict.TryGetValue(key, out var val) ? (TValue)val : defaultValue;

        public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            where TValue : new()
        {
            if (!dictionary.TryGetValue(key, out var val))
            {
                val = new TValue();
                dictionary[key] = val;
            }

            return val;
        }
        
        public static void AddToDictList<TList, TKey, TValue>(
            this IDictionary<TKey, TList> dict,
            TKey key,
            TValue value) where TList : IList<TValue>, new()
        {
            dict.GetOrCreate(key).Add(value);
        }

        public static bool GetOrFalse<TKey>(
            this IDictionary<TKey, object> dict,
            TKey key) =>
            dict.GetOrDefault(key, false);
    }
}
