using System.Linq;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Asyncifier
{
    public class ToArrayMethodMatcher : ISpecialAsyncifiableMethodMatcher
    {
        private readonly SemanticModel model;
        private readonly IMethodSymbol toArraySymbol;
        private readonly IMethodSymbol toArrayAsyncSymbol;
        private readonly INamedTypeSymbol queryableSymbol;

        public bool CanBeUsed { get; }
        
        public ToArrayMethodMatcher(SemanticModel model)
        {
            this.model = model;
            toArraySymbol = (IMethodSymbol)model.Compilation.GetTypeByMetadataName(typeof(Enumerable).FullName!)
                ?
                .GetMembers("ToArray")
                .First();
            toArrayAsyncSymbol = (IMethodSymbol)model.Compilation
                .GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions")
                ?
                .GetMembers("ToArrayAsync")
                .First();
            queryableSymbol = model.Compilation.GetTypeByMetadataName(typeof(IQueryable<>).FullName!);
            CanBeUsed = toArraySymbol != null && toArrayAsyncSymbol != null && queryableSymbol != null;
        }


        public bool TryGetAsyncMethod(InvocationExpressionSyntax node, out IMethodSymbol methodSymbol)
        {
            methodSymbol = null;
            
            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
                return false;

            if (!symbol.IsExtensionMethod || !symbol.ConstructedFrom.ReducedFrom.SymbolEquals(toArraySymbol))
                return false;

            if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            var accessedType = model.GetTypeInfo(memberAccess.Expression).Type;

            if (accessedType is not INamedTypeSymbol namedTypeSymbol)
                return false;

            if (!namedTypeSymbol.ConstructedFrom.SymbolEquals(queryableSymbol))
                return false;

            methodSymbol = toArrayAsyncSymbol;
            return true;
        }
    }
}
