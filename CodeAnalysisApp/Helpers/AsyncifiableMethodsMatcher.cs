using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Helpers
{
    public class AsyncifiableMethodsMatcher
    {
        private readonly Dictionary<IMethodSymbol,IMethodSymbol> asyncifiableMethodSymbols = new(SymbolEqualityComparer.Default);
        private readonly Compilation compilation;

        public AsyncifiableMethodsMatcher(Compilation compilation)
        {
            this.compilation = compilation;
        }

        public void FillAsyncifiableMethodsFromCompilation()
        {
            var provider = new AsyncifiableMethodsProvider(compilation);
            foreach (var symbolPair in provider.Provide())
            {
                asyncifiableMethodSymbols.Add(symbolPair.SyncMethod, symbolPair.AsyncMethod);
            }
        }

        public bool CanBeAsyncified(IMethodSymbol method) => asyncifiableMethodSymbols.ContainsKey(method);

        public bool TryGetAsyncMethod(IMethodSymbol method, out IMethodSymbol methodSymbol) => asyncifiableMethodSymbols.TryGetValue(method, out methodSymbol);
    }
}
