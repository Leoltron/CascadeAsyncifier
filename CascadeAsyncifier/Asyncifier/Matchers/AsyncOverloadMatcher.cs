using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Asyncifier.Matchers
{
    public class AsyncOverloadMatcher
    {
        private static readonly ConditionalWeakTable<Compilation, AsyncOverloadMatcher> instances = new();

        public static AsyncOverloadMatcher GetInstance(Compilation compilation)
        {
            lock (instances)
            {
                if (instances.TryGetValue(compilation, out var instance))
                    return instance;

                instance = new AsyncOverloadMatcher(compilation);
                instances.Add(compilation, instance);

                return instance;
            }
        }

        private readonly Dictionary<IMethodSymbol, IMethodSymbol> asyncifiableMethodSymbols =
            new(SymbolEqualityComparer.Default);

        private AsyncOverloadMatcher(Compilation compilation)
        {
            var provider = new AsyncOverloadsProvider(compilation);
            foreach (var symbolPair in provider.Provide())
            {
                asyncifiableMethodSymbols.TryAdd(symbolPair.SyncMethod, symbolPair.AsyncMethod);
            }
        }

        public bool HasAsyncOverload(IMethodSymbol method) =>
            asyncifiableMethodSymbols.ContainsKey(method.OriginalDefinition.ReducedFromOrItself());

        public bool TryGetAsyncMethod(IMethodSymbol method, out IMethodSymbol asyncMethod) =>
            asyncifiableMethodSymbols.TryGetValue(method.OriginalDefinition.ReducedFromOrItself(), out asyncMethod);
    }
}
