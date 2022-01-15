using System;
using System.Collections.Generic;
using System.Linq;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class OnlyAwaitInReturnAsyncMethodRewriter : InAsyncMethodContextRewriter
    {
        private bool InvalidForRefactoring
        {
            get => CurrentContext.GetOrDefault("InvalidForRefactoring", false);
            set => CurrentContext["InvalidForRefactoring"] = value;
        }

        private bool FoundAwaitReturn
        {
            get => CurrentContext.GetOrDefault("FoundAwaitReturn", false);
            set => CurrentContext["FoundAwaitReturn"] = value;
        }

        private bool DeasyncifyChildReturn
        {
            get => CurrentContext.GetOrDefault("DeasyncifyChildReturn", false);
            set => CurrentContext["DeasyncifyChildReturn"] = value;
        }

        private bool DeasyncifyReturn
        {
            get => CurrentContext.GetOrDefault("DeasyncifyReturn", false);
            set => CurrentContext["DeasyncifyReturn"] = value;
        }

       private void OnAwaitReturnFound()
       {
           if (DeasyncifyReturn)
               return;
           
           if (FoundAwaitReturn)
           {
               InvalidForRefactoring = true;
           }
           else
           {
               FoundAwaitReturn = true;
           }
       }

       private void OnRegularReturnFound()
       {
           if (DeasyncifyReturn)
               return;

           InvalidForRefactoring = true;
       }
       

        public override SyntaxNode VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            if (node.Parent is not ReturnStatementSyntax)
                InvalidForRefactoring = true;
            return base.VisitAwaitExpression(node);
        }

        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (DeasyncifyReturn)
            {
                return VisitAndDeasyncifyReturn(node);
            }
            
            var baseVisitedReturn = base.VisitReturnStatement(node);
            if (!InAsyncMethod || InvalidForRefactoring || !node.ChildNodes().Any())
                return baseVisitedReturn;
            
            if(node.ChildNodes().First() is not AwaitExpressionSyntax || FoundAwaitReturn){
                OnRegularReturnFound();
                return baseVisitedReturn;
            }

            OnAwaitReturnFound();

            return baseVisitedReturn;
        }

        private ReturnStatementSyntax VisitAndDeasyncifyReturn(ReturnStatementSyntax returnStatement)
        {
            if (returnStatement.Expression is not AwaitExpressionSyntax awaitExpression)
                throw new InvalidOperationException("Tried to deasyncify return without await expression at its root");

            var expression = Deasyncify(awaitExpression);

            return returnStatement.WithExpression((ExpressionSyntax)Visit(expression));
            
        }

        private static ExpressionSyntax Deasyncify(AwaitExpressionSyntax awaitExpression)
        {
            var expression = awaitExpression.Expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax memberAccessExpr
            } && memberAccessExpr.Name.Identifier.Text == "ConfigureAwait"
                ? memberAccessExpr.Expression
                : awaitExpression.Expression;

            return expression;
        }

        protected override void BeforeVisit(IDictionary<string, object> parentContext, IDictionary<string, object> nodeContext, SyntaxNode node)
        {
            nodeContext["DeasyncifyReturn"] = parentContext.GetOrDefault("DeasyncifyChildReturn", false);
        }

        protected override SyntaxNode AfterVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit)
        {
            if (DeasyncifyReturn || InvalidForRefactoring || !FoundAwaitReturn)
            {
                parentContext["DeasyncifyChildReturn"] = false;
                return nodeAfterVisit;
            }

            nodeContext["Revisit"] = true;
            parentContext["DeasyncifyChildReturn"] = true;

            return nodeAfterVisit;
        }

        protected override SyntaxNode VisitSimpleLambdaExpressionWithContext(SimpleLambdaExpressionSyntax node)
        {
            var visitedNode = base.VisitSimpleLambdaExpressionWithContext(node);
            return !DeasyncifyReturn ? visitedNode : ((SimpleLambdaExpressionSyntax)visitedNode).WithoutAsyncModifier();
        }

        protected override SyntaxNode VisitParenthesizedLambdaExpressionWithContext(ParenthesizedLambdaExpressionSyntax node)
        {
            var visitedNode = base.VisitParenthesizedLambdaExpressionWithContext(node);
            return !DeasyncifyReturn ? visitedNode : ((ParenthesizedLambdaExpressionSyntax)visitedNode).WithoutAsyncModifier();
        }

        protected override SyntaxNode VisitAnonymousMethodExpressionWithContext(AnonymousMethodExpressionSyntax node)
        {
            var visitedNode = base.VisitAnonymousMethodExpressionWithContext(node);
            return !DeasyncifyReturn ? visitedNode : ((AnonymousMethodExpressionSyntax)visitedNode).WithoutAsyncModifier();
        }

        protected override SyntaxNode VisitMethodDeclarationWithContext(MethodDeclarationSyntax node)
        {
            var visitedNode = base.VisitMethodDeclarationWithContext(node);
            return !DeasyncifyReturn ? visitedNode : ((MethodDeclarationSyntax)visitedNode).WithoutAsyncModifier();
        }


        protected override SyntaxNode VisitLocalFunctionStatementWithContext(LocalFunctionStatementSyntax node)
        {
            var visitedNode = base.VisitLocalFunctionStatementWithContext(node);
            return !DeasyncifyReturn ? visitedNode : ((LocalFunctionStatementSyntax)visitedNode).WithoutAsyncModifier().WithTriviaFrom(node);
        }
    }
}
