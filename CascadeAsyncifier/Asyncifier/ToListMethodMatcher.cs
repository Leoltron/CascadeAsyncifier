using System.Linq;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Asyncifier
{
    public class ToListMethodMatcher : ISpecialAsyncifiableMethodMatcher
    {
        private readonly SemanticModel model;
        private readonly IMethodSymbol toListSymbol;
        private readonly IMethodSymbol toListAsyncSymbol;
        private readonly INamedTypeSymbol queryableSymbol;

        public bool CanBeUsed { get; }
        
        public ToListMethodMatcher(SemanticModel model)
        {
            this.model = model;
            toListSymbol = (IMethodSymbol)model.Compilation.GetTypeByMetadataName(typeof(Enumerable).FullName!)
                ?
                .GetMembers("ToList")
                .First();
            toListAsyncSymbol = (IMethodSymbol)model.Compilation
                .GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions")
                ?
                .GetMembers("ToListAsync")
                .First();
            queryableSymbol = model.Compilation.GetTypeByMetadataName(typeof(IQueryable<>).FullName!);
            CanBeUsed = toListSymbol != null && toListAsyncSymbol != null && queryableSymbol != null;
        }


        public bool TryGetAsyncMethod(InvocationExpressionSyntax node, out IMethodSymbol methodSymbol)
        {
            methodSymbol = null;
            
            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
                return false;

            if (!symbol.IsExtensionMethod || !symbol.ConstructedFrom.ReducedFrom.SymbolEquals(toListSymbol))
                return false;

            if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            var accessedType = model.GetTypeInfo(memberAccess.Expression).Type;

            if (accessedType is not INamedTypeSymbol namedTypeSymbol)
                return false;

            if (!namedTypeSymbol.ConstructedFrom.SymbolEquals(queryableSymbol))
                return false;

            methodSymbol = toListAsyncSymbol;
            return true;
        }
    }
}
