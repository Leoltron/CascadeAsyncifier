using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
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
            var OldInAsyncMethod = InAsyncMethod;
            var OldCurrentMethod = CurrentMethod;
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = null;
            base.VisitSimpleLambdaExpression(node);
            InAsyncMethod = OldInAsyncMethod;
            CurrentMethod = OldCurrentMethod;
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            var OldInAsyncMethod = InAsyncMethod;
            var OldCurrentMethod = CurrentMethod;
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = null;
            base.VisitParenthesizedLambdaExpression(node);
            InAsyncMethod = OldInAsyncMethod;
            CurrentMethod = OldCurrentMethod;
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            var OldInAsyncMethod = InAsyncMethod;
            var OldCurrentMethod = CurrentMethod;
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = null;
            base.VisitAnonymousMethodExpression(node);
            InAsyncMethod = OldInAsyncMethod;
            CurrentMethod = OldCurrentMethod;
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var OldInAsyncMethod = InAsyncMethod;
            var OldCurrentMethod = CurrentMethod;
            InAsyncMethod = node.IsAsync();
            CurrentMethod = null;
            base.VisitLocalFunctionStatement(node);
            InAsyncMethod = OldInAsyncMethod;
            CurrentMethod = OldCurrentMethod;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var OldInAsyncMethod = InAsyncMethod;
            var OldCurrentMethod = CurrentMethod;
            InAsyncMethod = node.IsAsync();
            CurrentMethod = node;
            base.VisitMethodDeclaration(node);
            InAsyncMethod = OldInAsyncMethod;
            CurrentMethod = OldCurrentMethod;
        }
    }
}
