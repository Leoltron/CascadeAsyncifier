using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Rewriters.Base;
using CascadeAsyncifier.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CascadeAsyncifier.Rewriters
{
    public class UnawaitedInAsyncMethodCallRewriter : InAsyncMethodContextRewriter
    {
        private readonly AwaitableSyntaxChecker awaitableSyntaxChecker;

        public UnawaitedInAsyncMethodCallRewriter(SemanticModel semanticModel)
        {
            awaitableSyntaxChecker = new AwaitableSyntaxChecker(semanticModel);
        }

        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (!InAsyncMethod || node.IsInNoAwaitBlock() || node.Expression is AssignmentExpressionSyntax)
                return base.VisitExpressionStatement(node);

            if (!awaitableSyntaxChecker.IsTypeAwaitable(node.Expression))
                return base.VisitExpressionStatement(node);

            var awaitKeyword = Token(
                node.Expression.GetLeadingTrivia(),
                SyntaxKind.AwaitKeyword,
                TriviaList(Space));
            
            var awaitExpression =
                AwaitExpression(node.Expression.WithoutLeadingTrivia())
                    .WithAwaitKeyword(awaitKeyword);

            var newNode = node.WithExpression(awaitExpression);

            return base.VisitExpressionStatement(newNode);
        }
    }
}
