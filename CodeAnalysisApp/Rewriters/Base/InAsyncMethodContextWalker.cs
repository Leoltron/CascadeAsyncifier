using CodeAnalysisApp.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public abstract class InAsyncMethodContextWalker : CSharpSyntaxWalker
    {
        protected bool InAsyncMethod { get; private set; }
        protected MethodDeclarationSyntax CurrentMethod { get; private set; }


        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            var oldInAsyncMethod = InAsyncMethod;
            var oldCurrentMethod = CurrentMethod;
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = null;
            base.VisitSimpleLambdaExpression(node);
            InAsyncMethod = oldInAsyncMethod;
            CurrentMethod = oldCurrentMethod;
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            var oldInAsyncMethod = InAsyncMethod;
            var oldCurrentMethod = CurrentMethod;
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = null;
            base.VisitParenthesizedLambdaExpression(node);
            InAsyncMethod = oldInAsyncMethod;
            CurrentMethod = oldCurrentMethod;
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            var oldInAsyncMethod = InAsyncMethod;
            var oldCurrentMethod = CurrentMethod;
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = null;
            base.VisitAnonymousMethodExpression(node);
            InAsyncMethod = oldInAsyncMethod;
            CurrentMethod = oldCurrentMethod;
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var oldInAsyncMethod = InAsyncMethod;
            var oldCurrentMethod = CurrentMethod;
            InAsyncMethod = node.IsAsync();
            CurrentMethod = null;
            base.VisitLocalFunctionStatement(node);
            InAsyncMethod = oldInAsyncMethod;
            CurrentMethod = oldCurrentMethod;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var oldInAsyncMethod = InAsyncMethod;
            var oldCurrentMethod = CurrentMethod;
            InAsyncMethod = node.IsAsync();
            CurrentMethod = node;
            base.VisitMethodDeclaration(node);
            InAsyncMethod = oldInAsyncMethod;
            CurrentMethod = oldCurrentMethod;
        }
    }
}
