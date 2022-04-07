using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Rewriters.Base
{
    [SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
    public abstract class FunctionContextRewriter : CSharpSyntaxRewriter
    {
        private IDictionary<string, object> currentContext;
        public int MaxDepth { get; set; } = int.MaxValue; 

        protected IDictionary<string, object> CurrentContext
        {
            get => currentContext ??= new Dictionary<string, object>();
            private set => currentContext = value;
        }
        
        protected int FunctionDepth
        {
            get => CurrentContext.GetOrDefault("FunctionDepth", 0);
            set => CurrentContext["FunctionDepth"] = 0;
        }

        public sealed override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) =>
            ChangeContextAndVisit(
                BeforeSimpleLambdaExpressionVisit,
                VisitSimpleLambdaExpressionWithContext,
                AfterSimpleLambdaExpressionVisit,
                node);

        protected virtual void BeforeSimpleLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SimpleLambdaExpressionSyntax node)
        {
        }

        protected virtual SyntaxNode AfterSimpleLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit) =>
            nodeAfterVisit;


        protected virtual SyntaxNode VisitSimpleLambdaExpressionWithContext(SimpleLambdaExpressionSyntax node) =>
            base.VisitSimpleLambdaExpression(node);

        public sealed override SyntaxNode
            VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) =>
            ChangeContextAndVisit(
                BeforeParenthesizedLambdaExpressionVisit,
                VisitParenthesizedLambdaExpressionWithContext,
                AfterParenthesizedLambdaExpressionVisit,
                node);

        protected virtual void BeforeParenthesizedLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            ParenthesizedLambdaExpressionSyntax node)
        {
        }

        protected virtual SyntaxNode AfterParenthesizedLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit) =>
            nodeAfterVisit;


        protected virtual SyntaxNode VisitParenthesizedLambdaExpressionWithContext(
            ParenthesizedLambdaExpressionSyntax node) =>
            base.VisitParenthesizedLambdaExpression(node);

        public sealed override SyntaxNode VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) =>
            ChangeContextAndVisit(
                BeforeAnonymousMethodExpressionVisit,
                VisitAnonymousMethodExpressionWithContext,
                AfterAnonymousMethodExpressionVisit,
                node);

        protected virtual void BeforeAnonymousMethodExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            AnonymousMethodExpressionSyntax node)
        {
        }

        protected virtual SyntaxNode AfterAnonymousMethodExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit) =>
            nodeAfterVisit;


        protected virtual SyntaxNode VisitAnonymousMethodExpressionWithContext(AnonymousMethodExpressionSyntax node) =>
            base.VisitAnonymousMethodExpression(node);

        public sealed override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) =>
            ChangeContextAndVisit(
                BeforeMethodDeclarationVisit,
                VisitMethodDeclarationWithContext,
                AfterMethodDeclarationVisit,
                node);

        protected virtual void BeforeMethodDeclarationVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            MethodDeclarationSyntax node)
        {
        }

        protected virtual SyntaxNode AfterMethodDeclarationVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit) =>
            nodeAfterVisit;


        protected virtual SyntaxNode VisitMethodDeclarationWithContext(MethodDeclarationSyntax node) =>
            base.VisitMethodDeclaration(node);

        public sealed override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) =>
            ChangeContextAndVisit(
                BeforeLocalFunctionStatementVisit,
                VisitLocalFunctionStatementWithContext,
                AfterLocalFunctionStatementVisit,
                node);

        protected virtual void BeforeLocalFunctionStatementVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            LocalFunctionStatementSyntax node)
        {
        }

        protected virtual SyntaxNode AfterLocalFunctionStatementVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit) =>
            nodeAfterVisit;


        protected virtual SyntaxNode VisitLocalFunctionStatementWithContext(LocalFunctionStatementSyntax node) =>
            base.VisitLocalFunctionStatement(node);


        protected virtual SyntaxNode AfterVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode nodeAfterVisit) =>
            nodeAfterVisit;

        protected virtual void BeforeVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SyntaxNode node)
        {
            nodeContext["FunctionDepth"] = parentContext.GetOrDefault("FunctionDepth", 0) + 1;
        }

        private const int MAX_REVISITS = 8;
        protected virtual SyntaxNode ChangeContextAndVisit<TNode>(
            Action<IDictionary<string, object>, IDictionary<string, object>, TNode> beforeVisit,
            Func<TNode, SyntaxNode> visit,
            Func<IDictionary<string, object>, IDictionary<string, object>, SyntaxNode, SyntaxNode> afterVisit,
            TNode node) where TNode : SyntaxNode
        {
            if (FunctionDepth >= MaxDepth)
                return node;
            
            var revisits = 0;
            while (true)
            {
                var parentContext = CurrentContext ?? new Dictionary<string, object>();
                CurrentContext = new Dictionary<string, object>();
                BeforeVisit(parentContext, CurrentContext, node);
                beforeVisit(parentContext, CurrentContext, node);
                var newNode = visit(node);
                newNode = afterVisit(parentContext, CurrentContext, newNode);
                var newSyntaxNode = AfterVisit(parentContext, CurrentContext, newNode);

                var revisit = CurrentContext.GetOrFalse("Revisit");
                CurrentContext = parentContext;

                if (!revisit)
                    return newSyntaxNode;

                revisits++;

                if (revisits > MAX_REVISITS)
                    throw new Exception("Too many revisits in a row of the same node: " + MAX_REVISITS);
            }
        }
    }
}
