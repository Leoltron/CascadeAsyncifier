using System.Linq;
using CodeAnalysisApp.Helpers.SyncAsyncMethodPairProviders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class UseAsyncMethodRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;
        private readonly ISyncAsyncMethodPairProvider syncAsyncMethodPairProvider;

        public UseAsyncMethodRewriter(SemanticModel model) : this(model, new HardcodeSyncAsyncMethodPairProvider())
        {
        }

        public UseAsyncMethodRewriter(SemanticModel model, ISyncAsyncMethodPairProvider syncAsyncMethodPairProvider)
        {
            this.model = model;
            this.syncAsyncMethodPairProvider = syncAsyncMethodPairProvider;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visitedNode = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);

            if (!InAsyncMethod)
                return visitedNode;

            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
                return visitedNode;

            var matchingMethod = syncAsyncMethodPairProvider.Provide().FirstOrDefault(m => m.MatchSyncMethod(symbol));

            if (matchingMethod == null)
                return visitedNode;

            var expression = (MemberAccessExpressionSyntax)visitedNode.Expression;
            var newExpression = expression.WithName(
                SyntaxFactory.IdentifierName(matchingMethod.ReplaceName(expression.Name.Identifier.Text)));

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
