using System.Linq;
using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Rewriters.Base;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Rewriters
{
    public class OnlyAwaitInAsyncLambdaRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;

        public OnlyAwaitInAsyncLambdaRewriter(SemanticModel model)
        {
            this.model = model;
        }

        protected override SyntaxNode VisitMethodDeclarationWithContext(MethodDeclarationSyntax node)
        {
            var lastNode = node.ChildNodes().LastOrDefault();

            if (lastNode is not ArrowExpressionClauseSyntax arrowExpression)
                return base.VisitMethodDeclarationWithContext(node);
            
            if(!TryDeasyncifyAwaitExpressionWrapper(arrowExpression, out var deasyncifiedArrowExp))
                return base.VisitMethodDeclarationWithContext(node);

            return node.ReplaceNode(arrowExpression, deasyncifiedArrowExp).WithoutAsyncModifier();
        }

        protected override SyntaxNode VisitLocalFunctionStatementWithContext(LocalFunctionStatementSyntax node)
        {
            var lastNode = node.ChildNodes().LastOrDefault();

            if (lastNode is not ArrowExpressionClauseSyntax arrowExpression)
                return base.VisitLocalFunctionStatementWithContext(node);
            
            if(!TryDeasyncifyAwaitExpressionWrapper(arrowExpression, out var deasyncifiedArrowExp))
                return base.VisitLocalFunctionStatementWithContext(node);

            return node.ReplaceNode(arrowExpression, deasyncifiedArrowExp).WithoutAsyncModifier().WithTriviaFrom(node);
        }

        protected override SyntaxNode VisitParenthesizedLambdaExpressionWithContext(
            ParenthesizedLambdaExpressionSyntax node)
        {
            if(!TryDeasyncifyAwaitExpressionWrapper(node, out var deasyncifiedNode))
                return base.VisitParenthesizedLambdaExpressionWithContext(node);

            return deasyncifiedNode.WithoutAsyncModifier();
        }

        protected override SyntaxNode VisitSimpleLambdaExpressionWithContext(SimpleLambdaExpressionSyntax node)
        {
            if(!TryDeasyncifyAwaitExpressionWrapper(node, out var deasyncifiedNode))
                return base.VisitSimpleLambdaExpressionWithContext(node);

            return deasyncifiedNode.WithoutAsyncModifier();
        }

        private bool TryDeasyncifyAwaitExpressionWrapper<TNode>(
            TNode arrowExpression,
            out TNode expression) where  TNode : SyntaxNode
        {
            if (arrowExpression.ChildNodes().Last() is not AwaitExpressionSyntax awaitExpression
                || !GetCurrentMethodReturnType(model).SymbolEquals(model.GetTypeInfo(awaitExpression).Type))
            {
                expression = null;

                return false;
            }

            expression = arrowExpression.ReplaceNode(
                awaitExpression,
                ((AwaitExpressionSyntax)base.Visit(awaitExpression)).Deasyncify());

            return true;
        }
    }
}
