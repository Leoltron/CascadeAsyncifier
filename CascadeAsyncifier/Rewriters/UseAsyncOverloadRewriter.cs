using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CascadeAsyncifier.Asyncifier.Matchers;
using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Helpers;
using CascadeAsyncifier.Rewriters.Base;
using CascadeAsyncifier.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace CascadeAsyncifier.Rewriters
{
    public class UseAsyncOverloadRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;
        private readonly AsyncOverloadMatcher matcher;
        private readonly ISpecialAsyncifiableMethodMatcher[] specialMatchers;
        private readonly INamedTypeSymbol cancellationTokenSymbol;

        public UseAsyncOverloadRewriter(SemanticModel model)
        {
            this.model = model;
            TestAttributeChecker.GetInstance(model.Compilation);
            matcher = AsyncOverloadMatcher.GetInstance(model.Compilation);
            specialMatchers = new ISpecialAsyncifiableMethodMatcher[] { new EntityFrameworkQueryableMethodMatcher(model) }
                .Where(m => m.CanBeUsed)
                .ToArray();
            cancellationTokenSymbol = model.Compilation.GetTypeByMetadataName(typeof(CancellationToken).FullName!);
        }

        private HashSet<string> usingDirectivesToAdd;

        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            usingDirectivesToAdd = new HashSet<string>();
            var cu = (CompilationUnitSyntax)base.VisitCompilationUnit(node);
            usingDirectivesToAdd.ExceptWith(cu!.Usings.Select(u => u.Name.ToString()));

            return cu.WithUsingDirectives(usingDirectivesToAdd.ToArray());
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visitedNode = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);

            if (!InAsyncMethod || visitedNode == null || node.IsInNoAwaitBlock())
                return visitedNode;

            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
                return visitedNode;


            if (!matcher.TryGetAsyncMethod(symbol, out var matchingMethod) && 
                !specialMatchers.Any(m => m.TryGetAsyncMethod(node, out matchingMethod)))
            {
                return visitedNode;
            }

            if (matchingMethod.IsExtensionMethod && !matchingMethod.ContainingNamespace.IsGlobalNamespace)
            {
                usingDirectivesToAdd.Add(matchingMethod.ContainingNamespace.GetFullName());
            }

            if (matchingMethod.Parameters.Length > symbol.Parameters.Length &&
                !matchingMethod.Parameters.Last().IsOptional &&
                matchingMethod.Parameters.Last().Type.SymbolEquals(cancellationTokenSymbol))
            {
                visitedNode = visitedNode.AddCancellationTokenNoneArgument();
                usingDirectivesToAdd.Add("System.Threading");
            }
            
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
                        expression);
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

            return SyntaxFactory.ParenthesizedExpression(nodeWithAwaitExpression.WithoutTrivia()).WithTriviaFrom(nodeWithAwaitExpression);
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
