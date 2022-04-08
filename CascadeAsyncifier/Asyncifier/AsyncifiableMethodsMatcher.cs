using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Asyncifier
{
    public class AsyncifiableMethodsMatcher
    {
        private static readonly ConditionalWeakTable<Compilation, AsyncifiableMethodsMatcher> instances = new();

        public static AsyncifiableMethodsMatcher GetInstance(Compilation compilation)
        {
            lock (instances)
            {
                if (instances.TryGetValue(compilation, out var instance))
                    return instance;

                instance = new AsyncifiableMethodsMatcher(compilation);
                instances.Add(compilation, instance);

                return instance;
            }
        }

        private readonly Dictionary<IMethodSymbol, IMethodSymbol> asyncifiableMethodSymbols =
            new(SymbolEqualityComparer.Default);

        private AsyncifiableMethodsMatcher(Compilation compilation)
        {
            var provider = new AsyncifiableMethodsProvider(compilation);
            foreach (var symbolPair in provider.Provide())
            {
                asyncifiableMethodSymbols.TryAdd(symbolPair.SyncMethod, symbolPair.AsyncMethod);
            }
        }

        public bool CanBeAsyncified(IMethodSymbol method) =>
            asyncifiableMethodSymbols.ContainsKey(method.ReducedFrom ?? method.OriginalDefinition);

        public bool TryGetAsyncMethod(IMethodSymbol method, out IMethodSymbol methodSymbol) =>
            asyncifiableMethodSymbols.TryGetValue(method.ReducedFrom ?? method.OriginalDefinition, out methodSymbol);
    }
}
