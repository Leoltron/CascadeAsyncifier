using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Asyncifier
{
    public interface ISpecialAsyncifiableMethodMatcher
    {
        bool CanBeUsed { get; }
        bool TryGetAsyncMethod(InvocationExpressionSyntax node, out IMethodSymbol methodSymbol);
    }
}
