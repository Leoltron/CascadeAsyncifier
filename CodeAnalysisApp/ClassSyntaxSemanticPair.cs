using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp
{
    public class SyntaxSemanticPair<TNode, TSymbol> : IEquatable<SyntaxSemanticPair<TNode, TSymbol>> where TNode : SyntaxNode where TSymbol : ISymbol
    {
        public TNode Node { get; }
        public TSymbol Symbol { get; }

        public SyntaxSemanticPair(TNode node, TSymbol symbol)
        {
            Node = node;
            Symbol = symbol;
        }

        public bool Equals(SyntaxSemanticPair<TNode, TSymbol> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return EqualityComparer<TNode>.Default.Equals(Node, other.Node) && EqualityComparer<TSymbol>.Default.Equals(Symbol, other.Symbol);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;

            return Equals((SyntaxSemanticPair<TNode, TSymbol>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<TNode>.Default.GetHashCode(Node) * 397) ^ EqualityComparer<TSymbol>.Default.GetHashCode(Symbol);
            }
        }
    }

    public class ClassSyntaxSemanticPair : SyntaxSemanticPair<ClassDeclarationSyntax, ITypeSymbol>
    {
        public ClassSyntaxSemanticPair(ClassDeclarationSyntax node, ITypeSymbol symbol) : base(node, symbol)
        {
        }

        public static implicit operator ClassSyntaxSemanticPair((ClassDeclarationSyntax, ITypeSymbol) pair) =>
            new(pair.Item1, pair.Item2);
    }

    public class MethodSyntaxSemanticPair : SyntaxSemanticPair<MethodDeclarationSyntax, IMethodSymbol>
    {
        public MethodSyntaxSemanticPair(MethodDeclarationSyntax node, IMethodSymbol symbol) : base(node, symbol)
        {
        }

        public static implicit operator MethodSyntaxSemanticPair((MethodDeclarationSyntax, IMethodSymbol) pair) =>
            new(pair.Item1, pair.Item2);
    }
}
