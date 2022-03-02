using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Helpers.SyncAsyncMethodPairProviders;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Helpers
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
            methodCompareHelper = new MethodCompareHelper(new AwaitableChecker(compilation));
        }

        public IEnumerable<SyncAsyncMethodSymbolPair> Provide() => 
            GetAllTypes(compilation).SelectMany(FindSyncAsyncPairsInType);

        private IEnumerable<SyncAsyncMethodSymbolPair> FindSyncAsyncPairsInType(INamedTypeSymbol typeSymbol)
        {
            var syncAsyncMethodPairs =
                typesWithAsyncMethodPairs.GetOrDefault(typeSymbol.GetFullName(), new List<(string, string)>())
                                         .ToDictionary(p => p.Item1, p => p.Item2);
            
            var pairs = new List<string>();
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
                        pairs.Add(pair.ToString());
                        break;
                    } 
                    continue;
                }

                var matchingAsyncName = member.Name + "Async";
                var matchingAsyncMethod = asyncMembers
                                         .Where(am => am.Name == matchingAsyncName)
                                         .FirstOrDefault(a => methodCompareHelper.IsAsyncVersionOf(member, a));

                if (matchingAsyncMethod != null)
                {
                    var pair = new SyncAsyncMethodSymbolPair(member, matchingAsyncMethod);
                    yield return pair;
                    pairs.Add(pair.ToString());
                }
            }

            if (pairs.Any())
            {
                Debug.WriteLine(typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                Debug.WriteLine(string.Concat(pairs.Select(p => $"\t{p}\n")));
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation) =>
            GetAllTypes(compilation.GlobalNamespace);

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetTypeMembers())
            foreach (var nestedType in GetNestedTypes(type))
                yield return nestedType;

            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
            foreach (var type in GetAllTypes(nestedNamespace))
                yield return type;
        }

        private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nestedType in type.GetTypeMembers().SelectMany(GetNestedTypes))
                yield return nestedType;
        }
    }
}
