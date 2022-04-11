using System;
using System.Collections.Generic;
using System.Linq;
using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Rewriters.Base;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace CascadeAsyncifier.Rewriters
{
    public class UseSpecialAsyncMethodRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;
        private readonly IMethodSymbol toListSymbol;
        private readonly IMethodSymbol toListAsyncSymbol;
        private readonly INamedTypeSymbol queryableSymbol;

        public bool AllSymbolsFound { get; }

        public UseSpecialAsyncMethodRewriter(SemanticModel model)
        {
            this.model = model;
            toListSymbol = (IMethodSymbol)model.Compilation.GetTypeByMetadataName(typeof(Enumerable).FullName!)
                ?
                .GetMembers("ToList")
                .First();
            toListAsyncSymbol = (IMethodSymbol)model.Compilation
                .GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions")
                ?
                .GetMembers("ToListAsync")
                .First();
            queryableSymbol = model.Compilation.GetTypeByMetadataName(typeof(IQueryable<>).FullName!);
            AllSymbolsFound = toListSymbol != null && toListAsyncSymbol != null && queryableSymbol != null;
        }

        private HashSet<string> usingDirectivesToAdd;

        public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
        {
            if (!AllSymbolsFound)
                return node;

            usingDirectivesToAdd = new HashSet<string>();
            var cu = (CompilationUnitSyntax)base.VisitCompilationUnit(node);
            usingDirectivesToAdd.ExceptWith(cu!.Usings.Select(u => u.Name.ToString()));

            return cu.WithUsingDirectives(usingDirectivesToAdd.ToArray());
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!AllSymbolsFound)
                return node;

            var visitedNode = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);

            if (!InAsyncMethod || visitedNode == null || node.IsInNoAwaitBlock())
                return visitedNode;

            if (ModelExtensions.GetSymbolInfo(model, node).Symbol is not IMethodSymbol symbol)
                return visitedNode;

            if (!symbol.ConstructedFrom.ReducedFrom.SymbolEquals(toListSymbol))
                return visitedNode;

            if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
                return visitedNode;

            var accessedType = ModelExtensions.GetTypeInfo(model, memberAccess.Expression).Type;

            if (accessedType is not INamedTypeSymbol namedTypeSymbol)
                return visitedNode;

            if (!namedTypeSymbol.ConstructedFrom.SymbolEquals(queryableSymbol))
                return visitedNode;


            var awaitExpression = SyntaxNodesExtensions.ToAwaitExpression(
                memberAccess.WithName(GenerateName(memberAccess.Name, toListAsyncSymbol.Name)),
                visitedNode);
            var nodeWithAwaitExpression = visitedNode.WithExpression(awaitExpression);

            usingDirectivesToAdd.Add(toListAsyncSymbol.ContainingNamespace.GetFullName());

            if (visitedNode.Parent is not MemberAccessExpressionSyntax &&
                visitedNode.Parent is not ConditionalAccessExpressionSyntax)
                return nodeWithAwaitExpression;

            return SyntaxFactory.ParenthesizedExpression(nodeWithAwaitExpression.WithoutTrivia())
                .WithTriviaFrom(nodeWithAwaitExpression);
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
