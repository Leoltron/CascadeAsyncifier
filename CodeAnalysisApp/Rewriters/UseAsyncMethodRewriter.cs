using System;
using System.Collections.Concurrent;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeAnalysisApp.Rewriters
{
    public class UseAsyncMethodRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;
        private readonly AsyncifiableMethodsMatcher matcher;
        private static readonly ConcurrentDictionary<Compilation, AsyncifiableMethodsMatcher> Matchers = new();


        public UseAsyncMethodRewriter(SemanticModel model)
        {
            this.model = model;
            matcher = Matchers.GetOrAdd(model.Compilation, c =>
            {
                var matcher = new AsyncifiableMethodsMatcher(c);
                matcher.FillAsyncifiableMethodsFromCompilation();
                return matcher;
            });
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visitedNode = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);

            if (!InAsyncMethod || visitedNode == null)
                return visitedNode;

            if (ModelExtensions.GetSymbolInfo(model, node).Symbol is not IMethodSymbol symbol)
                return visitedNode;

            if (!matcher.TryGetAsyncMethod(symbol, out var matchingMethod))
                return visitedNode;

            var newName = SyntaxFactory.IdentifierName(matchingMethod.Name);

            ExpressionSyntax nodeWithAwaitExpression;
            switch (node.Expression)
            {
                case IdentifierNameSyntax:
                    nodeWithAwaitExpression = SyntaxNodesExtensions.ToAwaitExpression(
                        visitedNode.WithExpression(newName),
                        visitedNode);

                    break;
                case MemberAccessExpressionSyntax expression:
                {
                    var awaitExpression = SyntaxNodesExtensions.ToAwaitExpression(expression.WithName(newName), visitedNode);
                    nodeWithAwaitExpression = visitedNode.WithExpression(awaitExpression);

                    break;
                }
                default:
                    throw new ArgumentException();
            }

            if (visitedNode.Parent is not MemberAccessExpressionSyntax)
                return nodeWithAwaitExpression;

            return SyntaxFactory.ParenthesizedExpression(nodeWithAwaitExpression);
        }
    }
}
