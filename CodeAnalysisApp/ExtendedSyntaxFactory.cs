using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp
{
    internal static class ExtendedSyntaxFactory
    {
        internal static UsingDirectiveSyntax UsingDirective(params string[] identifierNames)
        {
            if (!identifierNames.Any())
                throw new InvalidOperationException("Sequence contains no elements");

            var identifierNameSyntaxList = identifierNames.Select(SyntaxFactory.IdentifierName).ToList();

            if (identifierNameSyntaxList.Count == 1)
                return UsingDirective(identifierNameSyntaxList.Single());

            var nameSyntax = identifierNameSyntaxList.Skip(2)
                .Aggregate(SyntaxFactory.QualifiedName(identifierNameSyntaxList[0], identifierNameSyntaxList[1]), SyntaxFactory.QualifiedName);

            return UsingDirective(nameSyntax);
        }
        
        internal static UsingDirectiveSyntax UsingDirective(NameSyntax nameSyntax)
        {
            return SyntaxFactory.UsingDirective(nameSyntax)
                .WithUsingKeyword(SyntaxFactory.Token(SyntaxKind.UsingKeyword).WithTrailingTrivia(SyntaxFactory.Space))
                .WithTrailingTrivia(SyntaxFactory.LineFeed);
        }
    }
}
