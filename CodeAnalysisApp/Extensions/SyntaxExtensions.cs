using System.Linq;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Extensions
{
    public static class SyntaxExtensions
    {
        public static bool HasAttribute(this MemberDeclarationSyntax node, SemanticModel model, ISymbol attributeSymbol)
        {
            return attributeSymbol != null &&
                   node.AttributeLists.SelectMany(a => a.Attributes)
                       .Select(a => model.GetTypeInfo(a).Type)
                       .Any(a => SymbolEqualityComparer.Default.Equals(attributeSymbol, a));
        }

        public static bool IsAsync(this MethodDeclarationSyntax methodDeclarationSyntax) =>
            methodDeclarationSyntax.Modifiers.Select(m => m.Kind()).Contains(SyntaxKind.AsyncKeyword);

        public static bool IsAsync(this LocalFunctionStatementSyntax methodDeclarationSyntax) =>
            methodDeclarationSyntax.Modifiers.Select(m => m.Kind()).Contains(SyntaxKind.AsyncKeyword);

        public static bool IsEmpty(this SyntaxToken token) => token.IsMissing || token.ValueText.IsNullOrWhiteSpace();

        public static ExpressionSyntax Deasyncify(this AwaitExpressionSyntax awaitExpression)
        {
            var expression = awaitExpression.Expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax memberAccessExpr
            } && memberAccessExpr.Name.Identifier.Text == "ConfigureAwait"
                ? memberAccessExpr.Expression
                : awaitExpression.Expression;

            return expression;
        }

        public static TNode GetFirstParentOfType<TNode>(this SyntaxNode node) where TNode : SyntaxNode
        {
            var currentParent = node.Parent;
            while (currentParent != null)
            {
                if (currentParent is TNode nodeToFind)
                    return nodeToFind;
                currentParent = currentParent.Parent;
            }

            return null;
        }
    }
}
