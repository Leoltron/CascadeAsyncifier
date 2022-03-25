using System.Linq;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp.Extensions
{
    public static class SyntaxNodesExtensions
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

        public static TNode LeadWithLineFeedIfNotPresent<TNode>(this TNode node) where TNode : SyntaxNode
        {
            
            var leadingTrivia = node.GetLeadingTrivia();
            var trailingTrivia = node.GetTrailingTrivia();

            return leadingTrivia.Concat(trailingTrivia).Count(e => e.IsKind(SyntaxKind.EndOfLineTrivia)) < 2 
                ? node.WithLeadingTrivia(leadingTrivia.Prepend(LineFeed)) 
                : node;
        }
        
        public static AwaitExpressionSyntax ToAwaitExpression(ExpressionSyntax expression, SyntaxNode nodeReplacedWithAwait)
        {
            var awaitKeyword = Token(
                nodeReplacedWithAwait.GetLeadingTrivia(),
                SyntaxKind.AwaitKeyword,
                TriviaList(Space));

            return AwaitExpression(expression.WithoutLeadingTrivia())
                .WithAwaitKeyword(awaitKeyword)
                .WithTrailingTrivia(nodeReplacedWithAwait.GetTrailingTrivia());
        }

        public static bool IsContainingFunctionADeclaredMethod(this SyntaxNode node)
        {
            while (node.Parent != null)
            {
                node = node.Parent;
                switch (node.Kind())
                {
                    case SyntaxKind.MethodDeclaration:
                        return true;
                    case SyntaxKind.LocalFunctionStatement:
                    case SyntaxKind.ParenthesizedLambdaExpression:
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.AnonymousMethodExpression:
                        return false;
                }
            }

            return false;
        }
        
        public static bool IsInNoAwaitBlock(this SyntaxNode node)
        {
            while (node != null)
            {
                switch (node.Kind())
                {
                    case SyntaxKind.LockStatement:
                        return true;
                    case SyntaxKind.MethodDeclaration:
                    case SyntaxKind.LocalFunctionStatement:
                    case SyntaxKind.ParenthesizedLambdaExpression:
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.AnonymousMethodExpression:
                        return false;
                }

                node = node.Parent;
            }

            return false;
        }
    }
}
