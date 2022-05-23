using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Rewriters.Base;
using CascadeAsyncifier.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Rewriters
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

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (!InAsyncMethod || node.IsInNoAwaitBlock())
                return base.VisitMemberAccessExpression(node);

            var expType = ModelExtensions.GetTypeInfo(semanticModel, node.Expression);

            if (node.Name.Identifier.Text == "Result" &&
                awaitableSyntaxChecker.IsGenericTask(expType.Type))
                return SyntaxNodesExtensions.ToAwaitExpression(node.Expression, node);

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!InAsyncMethod || node.IsInNoAwaitBlock() || node.Expression is not MemberAccessExpressionSyntax memberAccessNode)
                return base.VisitInvocationExpression(node);

            var shouldBeParenthesized = node.Parent is
                MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax;

            var identifierText = memberAccessNode.Name.Identifier.Text;
            var memberAccessExpression = memberAccessNode.Expression;

            ExpressionSyntax awaitableExpression;
            if (IsWait(identifierText, memberAccessExpression))
            {
                awaitableExpression = memberAccessExpression;
            }
            else if (IsGetAwaiterGetResult(identifierText, memberAccessExpression, out var innerMemberAccessNode))
            {
                awaitableExpression = innerMemberAccessNode.Expression;
            }
            else
            {
                return base.VisitInvocationExpression(node);
            }

            var expression = SyntaxNodesExtensions.ToAwaitExpression(awaitableExpression, node);

            if (shouldBeParenthesized)
                return SyntaxFactory.ParenthesizedExpression(expression);

            return expression;
        }

        private bool IsWait(string text, SyntaxNode node)
        {
            if (text != "Wait")
            {
                return false;
            }

            var nodeType = semanticModel.GetTypeInfo(node).Type;

            return awaitableSyntaxChecker.IsTask(nodeType);
        }

        private bool IsGetAwaiterGetResult(string text, ExpressionSyntax syntax,
                                           out MemberAccessExpressionSyntax innerAccessor)
        {
            innerAccessor = null;

            if (text != "GetResult")
            {
                return false;
            }

            if (syntax is not InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax memberAccessSyntax
                })
            {
                return false;
            }

            if (memberAccessSyntax.Name.Identifier.Text != "GetAwaiter")
            {
                return false;
            }

            if (!awaitableSyntaxChecker.IsTypeAwaitable(memberAccessSyntax.Expression))
            {
                return false;
            }

            innerAccessor = memberAccessSyntax;
            return true;
        }
    }
}
