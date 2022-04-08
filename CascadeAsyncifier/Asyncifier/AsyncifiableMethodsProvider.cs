using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Helpers;
using CascadeAsyncifier.Utils;
using Microsoft.CodeAnalysis;
using Serilog;

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
        private readonly AwaitableChecker awaitableChecker;

        public AsyncifiableMethodsProvider(Compilation compilation)
        {
            this.compilation = compilation;
            methodCompareHelper = new MethodCompareHelper(compilation);
            awaitableChecker = new AwaitableChecker(compilation);
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

        private IEnumerable<SyncAsyncMethodSymbolPair> FindAsyncExtensionsInType(INamedTypeSymbol typeSymbol)
        {
            if(!typeSymbol.IsStatic)
                yield break;
            foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                if(!method.IsExtensionMethod)
                    continue;
                
                if(!method.Name.EndsWith("Async"))
                    continue;
                
                if(!awaitableChecker.IsAnyTask(method.ReturnType))
                    continue;
                
                var targetType = method.Parameters.First().Type.OriginalDefinition;

                var syncMethodName = method.Name[..^5];
                var matchingSyncMethod =
                    targetType.GetMembers(syncMethodName)
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(otherMethod => methodCompareHelper.IsAsyncVersionOf(otherMethod, method, true));

                if (matchingSyncMethod != null)
                {
                    var pair = new SyncAsyncMethodSymbolPair(matchingSyncMethod, method);
Log.Warning("{Type}.{Sync} -> {Async}", typeSymbol.Name, matchingSyncMethod.Name, method.Name);
                    yield return pair;
                }
                else
                {
                    foreach (var syncMethod in compilation.GetSymbolsWithName(syncMethodName, SymbolFilter.Member)
                                 .OfType<IMethodSymbol>())
                    {
                        if(syncMethod.IsAsync || !syncMethod.IsExtensionMethod)
                            continue;

                        if (!methodCompareHelper.IsAsyncVersionOf(syncMethod, method, true, false))
                            continue;

                        var pair = new SyncAsyncMethodSymbolPair(syncMethod, method);
                        Log.Warning("{Type}.{Sync} -> {Async}", typeSymbol.Name, syncMethod.Name, method.Name);
                        yield return pair;
                        break;
                    }
                }
            }
        }
    }
}
