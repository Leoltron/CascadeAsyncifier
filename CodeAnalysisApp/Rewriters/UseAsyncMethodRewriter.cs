using System.Collections.Concurrent;
using System.Linq;
using CodeAnalysisApp.Helpers;
using CodeAnalysisApp.Helpers.SyncAsyncMethodPairProviders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class UseAsyncMethodRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;
        private readonly AsyncifiableMethodsMatcher matcher;
        private static readonly ConcurrentDictionary<Compilation, AsyncifiableMethodsMatcher> Matchers = new();


        public UseAsyncMethodRewriter(SemanticModel model)
        {
            this.model = model;
            matcher = Matchers.GetOrAdd(model.Compilation, c =>
            {
                var matcher = new AsyncifiableMethodsMatcher(c);
                matcher.FillAsyncifiableMethodsFromCompilation();
                return matcher;
            });
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visitedNode = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);

            if (!InAsyncMethod)
                return visitedNode;

            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
                return visitedNode;

            if (!matcher.TryGetAsyncMethod(symbol, out var matchingMethod))
                return visitedNode;

            var expression = (MemberAccessExpressionSyntax)visitedNode.Expression;
            var newExpression = expression.WithName(
                SyntaxFactory.IdentifierName(matchingMethod.Name));

            var awaitExpression = SyntaxFactory.AwaitExpression(newExpression.WithoutLeadingTrivia())
                .WithAwaitKeyword(
                    SyntaxFactory.Token(
                        visitedNode.GetLeadingTrivia(),
                        SyntaxKind.AwaitKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space)));

            var nodeWithAwaitExpression = visitedNode.WithExpression(awaitExpression);

            if (visitedNode.Parent is not MemberAccessExpressionSyntax)
                return nodeWithAwaitExpression;

            return SyntaxFactory.ParenthesizedExpression(nodeWithAwaitExpression);
        }
    }
}
