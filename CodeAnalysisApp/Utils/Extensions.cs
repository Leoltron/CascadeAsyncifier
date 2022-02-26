using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Utils
{
    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var element in source)
                action(element);
        }

        public static bool HasAttribute(this MemberDeclarationSyntax node, SemanticModel model, ISymbol attributeSymbol)
        {
            return attributeSymbol != null &&
                   node.AttributeLists.SelectMany(a => a.Attributes)
                       .Select(a => model.GetTypeInfo(a).Type)
                       .Any(a => SymbolEqualityComparer.Default.Equals(attributeSymbol, a));
        }

        public static bool IsAsync(this MethodDeclarationSyntax methodDeclarationSyntax) =>
            methodDeclarationSyntax.Modifiers.Select(m => m.Kind()).Contains(SyntaxKind.AsyncKeyword);

        public static bool IsAsync(this LocalFunctionStatementSyntax methodDeclarationSyntax) =>
            methodDeclarationSyntax.Modifiers.Select(m => m.Kind()).Contains(SyntaxKind.AsyncKeyword);

        public static bool SymbolEquals(this ISymbol one, ISymbol other)
        {
            return SymbolEqualityComparer.Default.Equals(one, other);
        }

        public static bool IsEmpty(this SyntaxToken token) => token.IsMissing || token.ValueText.IsNullOrWhiteSpace();
        
        public static ExpressionSyntax Deasyncify(this AwaitExpressionSyntax awaitExpression)
        {
            var expression = awaitExpression.Expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax memberAccessExpr
            } && memberAccessExpr.Name.Identifier.Text == "ConfigureAwait"
                ? memberAccessExpr.Expression
                : awaitExpression.Expression;

            return expression;
        }

        public static TNode GetFirstParentOfType<TNode>(this SyntaxNode node) where TNode : SyntaxNode
        {
            var currentParent = node.Parent;
            while (currentParent != null)
            {
                if (currentParent is TNode nodeToFind)
                    return nodeToFind;
                currentParent = currentParent.Parent;
            }

            return null;
        }

        public static bool SequencesEqual<T>(this IEnumerable<T> one, IEnumerable<T> other, Func<T, T, bool> comparer)
        {
            using var oneEnumerator = one.GetEnumerator();
            using var otherEnumerator = other.GetEnumerator();

            var oneHasNext = oneEnumerator.MoveNext();
            var otherHasNext = otherEnumerator.MoveNext();

            while (oneHasNext && otherHasNext)
            {
                if (!comparer(oneEnumerator.Current, otherEnumerator.Current))
                    return false;

                oneHasNext = oneEnumerator.MoveNext();
                otherHasNext = otherEnumerator.MoveNext();
            }

            return !oneHasNext && !otherHasNext;
        }

        public static (List<T> filtered, List<T> unfiltered) SplitByFilter<T>(
            this IEnumerable<T> source, Predicate<T> filter)
        {
            var filtered = new List<T>();
            var unfiltered = new List<T>();

            foreach (var element in source)
            {
                if(filter(element))
                    filtered.Add(element);
                else
                    unfiltered.Add(element);
            }

            return (filtered, unfiltered);
        }

        public static string GetFullName(this ISymbol symbol)
        {
            var nameParts = new List<string>();
            while (true)
            {
                nameParts.Add(symbol.Name);
                if (symbol.ContainingNamespace.IsGlobalNamespace)
                    break;
                symbol = symbol.ContainingNamespace;
            }

            nameParts.Reverse();
            return string.Join(".", nameParts);
        } 
    }
}
