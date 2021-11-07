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

        public static TValue AddOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            where TValue : new()
        {
            if (!dictionary.TryGetValue(key, out var val))
            {
                val = new TValue();
                dictionary[key] = val;
            }

            return val;
        }

        public static bool IsAsync(this MethodDeclarationSyntax methodDeclarationSyntax) => 
            methodDeclarationSyntax.Modifiers.Select(m => m.Kind()).Contains(SyntaxKind.AsyncKeyword);
        public static bool IsAsync(this LocalFunctionStatementSyntax methodDeclarationSyntax) => 
            methodDeclarationSyntax.Modifiers.Select(m => m.Kind()).Contains(SyntaxKind.AsyncKeyword);

        public static bool SymbolEquals(this ITypeSymbol one, ITypeSymbol other)
        {
            return SymbolEqualityComparer.Default.Equals(one, other);
        }

        public static bool IsEmpty(this SyntaxToken token) =>
            token.IsMissing || token.ValueText.IsNullOrWhiteSpace();
    }
}