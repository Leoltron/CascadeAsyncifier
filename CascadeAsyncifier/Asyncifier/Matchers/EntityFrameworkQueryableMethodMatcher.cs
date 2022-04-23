using System.Collections.Generic;
using System.Linq;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Asyncifier.Matchers
{
    public class EntityFrameworkQueryableMethodMatcher : ISpecialAsyncifiableMethodMatcher
    {
        private static readonly string[] SyncMethodNames =
        {
            "ToList", "ToArray", "ToDictionary", "Any", "All", "Count", "LongCount", 
            "First", "FirstOrDefault", "Single", "SingleOrDefault", "Last",
            "LastOrDefault", "Max", "Min", "Average", "Sum"
        };

        private readonly IDictionary<IMethodSymbol, IMethodSymbol> syncToAsync =
            new Dictionary<IMethodSymbol, IMethodSymbol>(SymbolEqualityComparer.Default);

        private readonly SemanticModel model;
        private readonly INamedTypeSymbol queryableSymbol;

        public bool CanBeUsed { get; }

        public EntityFrameworkQueryableMethodMatcher(SemanticModel model)
        {
            this.model = model;

            var enumerableType = model.Compilation.GetTypeByMetadataName(typeof(Enumerable).FullName!);
            var queryableType = model.Compilation.GetTypeByMetadataName(typeof(Queryable).FullName!);
            var queryableExtensionsType = model.Compilation
                                                              .GetTypeByMetadataName(
                                                                   "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions");
            queryableSymbol = model.Compilation.GetTypeByMetadataName(typeof(IQueryable<>).FullName!);

            if (queryableSymbol == null || enumerableType == null || queryableType == null || queryableExtensionsType == null)
            {
                CanBeUsed = false;
                return;
            }
            
            foreach (var syncMethodName in SyncMethodNames)
            {
                var asyncMethod = queryableExtensionsType.GetMembers(syncMethodName + "Async").OfType<IMethodSymbol>().First();
                foreach (var syncMethod in enumerableType.GetMembers(syncMethodName).OfType<IMethodSymbol>())
                {
                    syncToAsync[syncMethod] = asyncMethod;
                }
                foreach (var syncMethod in queryableType.GetMembers(syncMethodName).OfType<IMethodSymbol>())
                {
                    syncToAsync[syncMethod] = asyncMethod;
                }
            }
            CanBeUsed = true;
        }


        public bool TryGetAsyncMethod(InvocationExpressionSyntax node, out IMethodSymbol methodSymbol)
        {
            methodSymbol = null;

            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
                return false;

            if (!symbol.IsExtensionMethod || !syncToAsync.TryGetValue(symbol.ConstructedFrom.ReducedFromOrItself(), out var asyncSymbol))
                return false;

            if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            var accessedType = model.GetTypeInfo(memberAccess.Expression).Type;

            if (accessedType is not INamedTypeSymbol namedTypeSymbol)
                return false;

            if (!namedTypeSymbol.ConstructedFrom.SymbolEquals(queryableSymbol))
                return false;

            methodSymbol = asyncSymbol;
            return true;
        }
    }
}
