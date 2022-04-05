using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeAnalysisApp.Extensions
{
    public static class EnumerableExtensions
    {
        public static bool SequencesEqual<T>(this IEnumerable<T> one, IEnumerable<T> other, Func<T, T, bool> comparer)
        {
            using var oneEnumerator = one.GetEnumerator();
            using var otherEnumerator = other.GetEnumerator();

            var oneHasNext = oneEnumerator.MoveNext();
            var otherHasNext = otherEnumerator.MoveNext();

            while (oneHasNext && otherHasNext)
            {
                if (!comparer(oneEnumerator.Current, otherEnumerator.Current))
                    return false;

                oneHasNext = oneEnumerator.MoveNext();
                otherHasNext = otherEnumerator.MoveNext();
            }

            return !oneHasNext && !otherHasNext;
        }

        public static (List<T> filtered, List<T> unfiltered) SplitByFilter<T>(
            this IEnumerable<T> source, Predicate<T> filter)
        {
            var filtered = new List<T>();
            var unfiltered = new List<T>();

            foreach (var element in source)
            {
                if (filter(element))
                    filtered.Add(element);
                else
                    unfiltered.Add(element);
            }

            return (filtered, unfiltered);
        }

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source) =>
            source.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
