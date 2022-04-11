using System;
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

        public IEnumerable<SyncAsyncMethodSymbolPair> Provide()
        {
            var extensionMethods = new Dictionary<INamedTypeSymbol, List<IMethodSymbol>>(SymbolEqualityComparer.Default);
            
            return compilation.GlobalNamespace.GetAllTypes()
                .OrderBy(s => s.IsStatic ? 0 : 1)
                .SelectMany(symbol => FindSyncAsyncPairsInType(symbol, extensionMethods));
        }

        private IEnumerable<SyncAsyncMethodSymbolPair> FindSyncAsyncPairsInType(
            INamedTypeSymbol typeSymbol,
            Dictionary<INamedTypeSymbol, List<IMethodSymbol>> extensionMethods)
        {
            var syncAsyncMethodPairs =
                typesWithAsyncMethodPairs.GetOrDefault(typeSymbol.GetFullName(), new List<(string, string)>())
                                         .ToDictionary(p => p.Item1, p => p.Item2);

            var typeExtensionMethods = (IList<IMethodSymbol>)extensionMethods.GetOrDefault(typeSymbol) ?? Array.Empty<IMethodSymbol>();

            var methods = typeExtensionMethods.Concat(typeSymbol.GetMembers().OfType<IMethodSymbol>()).ToList();
           
            foreach (var method in methods)
            {
                if (method.IsAsync || method.Name.EndsWith("Async"))
                {
                    if (method.IsExtensionMethod && method.Parameters.First().Type.OriginalDefinition is INamedTypeSymbol type)
                    {
                        extensionMethods.GetOrCreate(type).Add(method);
                    }
                    continue;
                }

                if (syncAsyncMethodPairs.TryGetValue(method.Name, out var asyncName))
                {
                    foreach (var asyncMethod in typeSymbol.GetMembers(asyncName).OfType<IMethodSymbol>())
                    {
                        if (asyncMethod.SymbolEquals(method) || !methodCompareHelper.IsAsyncVersionOf(method, asyncMethod, true, false))
                            continue;
                        
                        var pair = new SyncAsyncMethodSymbolPair(method, asyncMethod);
                        yield return pair;
                        break;
                    } 
                    continue;
                }

                var asyncMethodName = method.Name + "Async";
                var matchingAsyncMethod = methods.Where(e => e.Name == asyncMethodName)
                    .FirstOrDefault(otherMethod => methodCompareHelper.IsAsyncVersionOf(method, otherMethod, true, true));

                if (matchingAsyncMethod != null)
                {
                    var pair = new SyncAsyncMethodSymbolPair(method, matchingAsyncMethod);
                    yield return pair;
                }else if (method.IsExtensionMethod && method.Parameters.First().Type.OriginalDefinition is INamedTypeSymbol type)
                {
                    extensionMethods.GetOrCreate(type).Add(method);
                }
            }
        }
    }
}
