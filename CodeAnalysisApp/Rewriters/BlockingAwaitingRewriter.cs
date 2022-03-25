using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

            var expType = ModelExtensions.GetTypeInfo(semanticModel, node.Expression);

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

            var shouldBeParenthesized = node.Parent is MemberAccessExpressionSyntax ||
                                        node.Parent is ConditionalAccessExpressionSyntax;

            var identifierText = memberAccessNode.Name.Identifier.Text;
            var memberAccessExpression = memberAccessNode.Expression;
            if (identifierText == "Wait")
            {
                var expType = ModelExtensions.GetTypeInfo(semanticModel, memberAccessExpression).Type;

                if (awaitableSyntaxChecker.IsTask(expType))
                {
                    var expression = SyntaxNodesExtensions.ToAwaitExpression(memberAccessExpression, node);

                    if(shouldBeParenthesized)
                        return SyntaxFactory.ParenthesizedExpression(expression);

                    return expression;
                }
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
                    var expression = SyntaxNodesExtensions.ToAwaitExpression(innerMemberAccessNode.Expression, node);

                    if(shouldBeParenthesized)
                        return SyntaxFactory.ParenthesizedExpression(expression);
                    
                    return expression;
                }
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
