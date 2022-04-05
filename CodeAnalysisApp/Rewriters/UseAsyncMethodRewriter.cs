using System;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Helpers;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeAnalysisApp.Rewriters
{
    public class UseAsyncMethodRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;
        private readonly AsyncifiableMethodsMatcher matcher;

        public UseAsyncMethodRewriter(SemanticModel model)
        {
            this.model = model;
            TestAttributeChecker.GetInstance(model.Compilation);
            matcher = AsyncifiableMethodsMatcher.GetInstance(model.Compilation);
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visitedNode = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);

            if (!InAsyncMethod || visitedNode == null || node.IsInNoAwaitBlock())
                return visitedNode;

            if (ModelExtensions.GetSymbolInfo(model, node).Symbol is not IMethodSymbol symbol)
                return visitedNode;

            if (!matcher.TryGetAsyncMethod(symbol, out var matchingMethod))
                return visitedNode;

            var newName = SyntaxFactory.IdentifierName(matchingMethod.Name);

            ExpressionSyntax nodeWithAwaitExpression;
            switch (node.Expression)
            {
                case GenericNameSyntax genericName:
                    nodeWithAwaitExpression = SyntaxNodesExtensions.ToAwaitExpression(
                        visitedNode.WithExpression(genericName.WithIdentifier(SyntaxFactory.Identifier(matchingMethod.Name))),
                        visitedNode);

                    break;
                case IdentifierNameSyntax:
                    nodeWithAwaitExpression = SyntaxNodesExtensions.ToAwaitExpression(
                        visitedNode.WithExpression(newName),
                        visitedNode);

                    break;
                case MemberAccessExpressionSyntax expression:
                {
                    var awaitExpression = SyntaxNodesExtensions.ToAwaitExpression(
                         expression.WithName(GenerateName(expression.Name, matchingMethod.Name)),
                        visitedNode);
                    nodeWithAwaitExpression = visitedNode.WithExpression(awaitExpression);

                    break;
                }
                case MemberBindingExpressionSyntax:
                    LogHelper.ManualAsyncificationRequired(node.GetLocation(), symbol.Name);
                    //Notify that user input might be needed

                    return visitedNode;
                default:
                    throw new ArgumentException();
            }

            if (visitedNode.Parent is not MemberAccessExpressionSyntax && visitedNode.Parent is not ConditionalAccessExpressionSyntax)
                return nodeWithAwaitExpression;

            return SyntaxFactory.ParenthesizedExpression(nodeWithAwaitExpression);
        }

        private static SimpleNameSyntax GenerateName(SimpleNameSyntax prevName, string newName) =>
            prevName switch
            {
                IdentifierNameSyntax => SyntaxFactory.IdentifierName(newName),
                GenericNameSyntax genericNameSyntax => genericNameSyntax.WithIdentifier(
                    SyntaxFactory.Identifier(newName)),
                _ => throw new ArgumentException()
            };
    }
}
