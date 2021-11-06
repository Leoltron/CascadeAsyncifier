using System;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public abstract class InAsyncMethodContextRewriter : CSharpSyntaxRewriter
    {
        protected bool InAsyncMethod { get; private set; }
        public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => 
            ChangeAsyncAndVisit(!node.AsyncKeyword.IsEmpty(), base.VisitSimpleLambdaExpression, node);

        public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)=> 
            ChangeAsyncAndVisit(!node.AsyncKeyword.IsEmpty(), base.VisitParenthesizedLambdaExpression, node);

        public override SyntaxNode VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => 
            ChangeAsyncAndVisit(!node.AsyncKeyword.IsEmpty(), base.VisitAnonymousMethodExpression, node);

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) => 
            ChangeAsyncAndVisit(node.IsAsync(), base.VisitMethodDeclaration, node);

        public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) =>
            ChangeAsyncAndVisit(node.IsAsync(), base.VisitLocalFunctionStatement, node);

        private SyntaxNode ChangeAsyncAndVisit<T>(bool newInAsyncMethod, Func<T, SyntaxNode> innerVisitor, T arg)
        {
            var oldInAsyncMethod = InAsyncMethod;
            InAsyncMethod = newInAsyncMethod;
            var node = innerVisitor(arg);
            InAsyncMethod = oldInAsyncMethod;

            return node;
        }
    }
}
