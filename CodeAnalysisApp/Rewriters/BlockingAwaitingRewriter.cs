using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class BlockingAwaitingRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel semanticModel;
        private readonly AwaitableSyntaxChecker awaitableSyntaxChecker;

        public BlockingAwaitingRewriter(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
            awaitableSyntaxChecker = new AwaitableSyntaxChecker(semanticModel);
        }

        public override SyntaxNode 
            VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (!InAsyncMethod)
                return base.VisitMemberAccessExpression(node);

            var expType = semanticModel.GetTypeInfo(node.Expression);

            if (node.Name.Identifier.Text == "Result" &&
                awaitableSyntaxChecker.IsGenericTask(expType.Type))
                return SyntaxNodesExtensions.ToAwaitExpression(node.Expression, node);

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode 
            VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!InAsyncMethod ||
                node.Expression is not MemberAccessExpressionSyntax memberAccessNode)
                return base.VisitInvocationExpression(node);

            var identifierText = memberAccessNode.Name.Identifier.Text;
            var memberAccessExpression = memberAccessNode.Expression;
            if (identifierText == "Wait")
            {
                var expType = semanticModel.GetTypeInfo(memberAccessExpression).Type;

                if (awaitableSyntaxChecker.IsTask(expType))
                    return SyntaxNodesExtensions.ToAwaitExpression(memberAccessExpression, node);
            }
            else if (identifierText == "GetResult")
            {
                if (memberAccessExpression is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax innerMemberAccessNode
                    } &&
                    innerMemberAccessNode.Name.Identifier.Text == "GetAwaiter" &&
                    awaitableSyntaxChecker.IsTypeAwaitable(innerMemberAccessNode.Expression))
                {
                    return SyntaxNodesExtensions.ToAwaitExpression(innerMemberAccessNode.Expression, node);
                }
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
