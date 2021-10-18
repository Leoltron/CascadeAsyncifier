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

            var identifierNameSyntaxes = identifierNames.Select(IdentifierName).ToList();

            if (identifierNameSyntaxes.Count == 1)
                return SyntaxFactory.UsingDirective(identifierNameSyntaxes.Single());

            return SyntaxFactory.UsingDirective(
                identifierNameSyntaxes.Skip(2)
                    .Aggregate(QualifiedName(identifierNameSyntaxes[0], identifierNameSyntaxes[1]), QualifiedName));
        }
    }
}
