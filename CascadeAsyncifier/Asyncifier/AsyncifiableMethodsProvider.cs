using System.Collections.Generic;
using System.Linq;
using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Helpers;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Asyncifier
{
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
            
            var members = typeSymbol.GetMembers().OfType<IMethodSymbol>().ToList();
            var (asyncMembers, syncMembers) = members.SplitByFilter(m => m.IsAsync || m.Name.EndsWith("Async"));
            foreach (var member in syncMembers)
            {
                if (syncAsyncMethodPairs.TryGetValue(member.Name, out var asyncName))
                {
                    foreach (var asyncMethod in members.Where(m => m.Name == asyncName))
                    {
                        if (asyncMethod.SymbolEquals(member) || !methodCompareHelper.IsAsyncVersionOf(member, asyncMethod, true))
                            continue;
                        
                        var pair = new SyncAsyncMethodSymbolPair(member, asyncMethod);
                        yield return pair;
                        break;
                    } 
                    continue;
                }

                var matchingAsyncName = member.Name + "Async";
                var matchingAsyncMethod = asyncMembers
                                         .FirstOrDefault(a =>  a.Name == matchingAsyncName && methodCompareHelper.IsAsyncVersionOf(member, a));

                if (matchingAsyncMethod != null)
                {
                    var pair = new SyncAsyncMethodSymbolPair(member, matchingAsyncMethod);
                    yield return pair;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation) =>
            compilation.GlobalNamespace.GetAllTypes();
    }
}
