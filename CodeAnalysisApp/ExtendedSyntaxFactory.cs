using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp
{
    internal static class ExtendedSyntaxFactory
    {
        internal static UsingDirectiveSyntax UsingDirective(params string[] identifierNames)
        {
            if (!identifierNames.Any())
                throw new InvalidOperationException("Sequence contains no elements");

            var identifierNameSyntaxList = identifierNames.Select(IdentifierName).ToList();

            if (identifierNameSyntaxList.Count == 1)
                return SyntaxFactory.UsingDirective(identifierNameSyntaxList.Single());

            return SyntaxFactory.UsingDirective(
                identifierNameSyntaxList.Skip(2)
                    .Aggregate(QualifiedName(identifierNameSyntaxList[0], identifierNameSyntaxList[1]), QualifiedName));
        }
    }
}
