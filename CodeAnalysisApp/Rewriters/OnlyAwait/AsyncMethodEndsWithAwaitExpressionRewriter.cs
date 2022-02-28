using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class AsyncMethodEndsWithAwaitExpressionRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;
        private readonly INamedTypeSymbol taskSymbol;

        public AsyncMethodEndsWithAwaitExpressionRewriter(SemanticModel model)
        {
            this.model = model;
            taskSymbol = model.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
        }
        
        private bool FoundReturn
        {
            get => CurrentContext.GetOrFalse("FoundReturn");
            set => CurrentContext["FoundReturn"] = value;
        }
        
        private int AwaitExpressionsFound
        {
            get => CurrentContext.GetOrDefault("AwaitExpressionsFound" ,0);
            set => CurrentContext["AwaitExpressionsFound"] = value;
        }

        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
        {
            FoundReturn = true;
            return base.VisitReturnStatement(node);
        }

        public override SyntaxNode VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            AwaitExpressionsFound++;
            return base.VisitAwaitExpression(node);
        }

        protected override SyntaxNode AfterMethodDeclarationVisit(IDictionary<string, object> parentContext, IDictionary<string, object> nodeContext, SyntaxNode nodeAfterVisit)
        {
            var returnType = model.GetDeclaredSymbol(nodeAfterVisit);

            if (returnType is not IMethodSymbol ms || !ms.ReturnType.SymbolEquals(taskSymbol))
                return nodeAfterVisit;
            
            return AfterVisit<MethodDeclarationSyntax>(
                nodeContext,
                nodeAfterVisit,
                m => m.WithoutAsyncModifier());
        }

        private static SyntaxNode AfterVisit<TNode>(
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit,
            Func<TNode, TNode> removeAsyncModifier) where TNode : SyntaxNode
        {
            if (nodeAfterVisit is not TNode tNode)
                return nodeAfterVisit;
            
            if (nodeContext.GetOrFalse("FoundReturn"))
                return nodeAfterVisit;

            if(nodeContext.GetOrDefault("AwaitExpressionsFound" , 0) != 1)
                return nodeAfterVisit;
                
            
            var lastNode = tNode.ChildNodes().LastOrDefault();
            if(lastNode is not BlockSyntax)
                return nodeAfterVisit;

            var lastNodeInBlock = lastNode.ChildNodes().LastOrDefault();
            if(lastNodeInBlock is not ExpressionStatementSyntax expressionSyntax)
                return nodeAfterVisit;

            if (expressionSyntax.Expression is not AwaitExpressionSyntax awaitExpressionSyntax)
                return nodeAfterVisit;

            var nodeWithReturn = tNode.ReplaceNode(
                expressionSyntax,
                SyntaxFactory.ReturnStatement(awaitExpressionSyntax.Deasyncify()).NormalizeWhitespace().WithTriviaFrom(expressionSyntax));

            return removeAsyncModifier(nodeWithReturn).WithTriviaFrom(nodeAfterVisit);

        }
    }
}
