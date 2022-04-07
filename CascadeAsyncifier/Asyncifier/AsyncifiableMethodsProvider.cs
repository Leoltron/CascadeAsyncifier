using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Helpers;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Asyncifier
{
    [SuppressMessage("ReSharper", "ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator")]
    public class AsyncifiableMethodsProvider
    {
        private readonly Compilation compilation;

        private readonly IDictionary<string, List<(string, string)>> typesWithAsyncMethodPairs =
            AsyncifiableMethodsUnusualNamesProvider.Provide()
                                                   .GroupBy(p => p.typeName)
                                                   .ToDictionary(g => g.Key, g => g.Select( p=>(p.syncName, p.asyncName)).ToList());

        private readonly MethodCompareHelper methodCompareHelper;

        public AsyncifiableMethodsProvider(Compilation compilation)
        {
            this.compilation = compilation;
            methodCompareHelper = new MethodCompareHelper(compilation);
        }

        public IEnumerable<SyncAsyncMethodSymbolPair> Provide() => 
            GetAllTypes(compilation).SelectMany(FindSyncAsyncPairsInType);

        private IEnumerable<SyncAsyncMethodSymbolPair> FindSyncAsyncPairsInType(INamedTypeSymbol typeSymbol)
        {
            var syncAsyncMethodPairs =
                typesWithAsyncMethodPairs.GetOrDefault(typeSymbol.GetFullName(), new List<(string, string)>())
                                         .ToDictionary(p => p.Item1, p => p.Item2);
            
            var methods = typeSymbol.GetMembers().OfType<IMethodSymbol>().ToList();
            foreach (var method in methods)
            {
                if (method.IsAsync || method.Name.EndsWith("Async"))
                    continue;
                
                if (syncAsyncMethodPairs.TryGetValue(method.Name, out var asyncName))
                {
                    foreach (var asyncMethod in methods.Where(m => m.Name == asyncName))
                    {
                        if (asyncMethod.SymbolEquals(method) || !methodCompareHelper.IsAsyncVersionOf(method, asyncMethod, true))
                            continue;
                        
                        var pair = new SyncAsyncMethodSymbolPair(method, asyncMethod);
                        yield return pair;
                        break;
                    } 
                    continue;
                }

                var matchingAsyncMethod = typeSymbol.GetMembers(method.Name + "Async")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(otherMethod => methodCompareHelper.IsAsyncVersionOf(method, otherMethod));

                if (matchingAsyncMethod != null)
                {
                    var pair = new SyncAsyncMethodSymbolPair(method, matchingAsyncMethod);
                    yield return pair;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation) =>
            compilation.GlobalNamespace.GetAllTypes();
    }
}
