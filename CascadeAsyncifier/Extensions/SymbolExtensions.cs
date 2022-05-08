using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Extensions
{
    public static class SymbolExtensions
    {
        public static bool SymbolEquals(this ISymbol one, ISymbol other) => SymbolEqualityComparer.Default.Equals(one, other);

        public static string GetFullName(this ISymbol symbol) => 
            string.Join(".", symbol.GetFullNamePartsReversed().Reverse());

        private static IEnumerable<string> GetFullNamePartsReversed(this ISymbol symbol)
        {
            while (true)
            {
                yield return symbol.Name;
                if (symbol.ContainingNamespace.IsGlobalNamespace)
                    yield break;
                symbol = symbol.ContainingNamespace;
            }
        }

        public static IMethodSymbol FindOverridenOrImplementedSymbol(this IMethodSymbol methodSymbol)
        {
            if (methodSymbol.IsOverride)
                return methodSymbol.OverriddenMethod;

            var containingType = methodSymbol.ContainingType;
            return containingType.AllInterfaces
                                 .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
                                 .FirstOrDefault(interfaceMethod =>
                                                     containingType
                                                        .FindImplementationForInterfaceMember(interfaceMethod)
                                                        .SymbolEquals(methodSymbol));
        }
        
        public static bool WholeHierarchyChainIsInSourceCode(this IMethodSymbol method)
        {
            do
            {
                if (!method.DeclaringSyntaxReferences.Any())
                    return false;
            } while ((method = method.FindOverridenOrImplementedSymbol()) != null);

            return true;
        } 
        
        public static bool InheritsFromOrEquals(this ITypeSymbol type, ITypeSymbol baseType)
        {
            return type.GetBaseTypesAndThis().Any(t => SymbolEqualityComparer.Default.Equals(t, baseType));
        }
        
        public static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type)
        {
            var current = type;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
        }

        public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetTypeMembers())
            foreach (var nestedType in GetNestedTypes(type))
                yield return nestedType;

            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
            foreach (var type in GetAllTypes(nestedNamespace))
                yield return type;
        }

        public static IEnumerable<INamespaceSymbol> GetNamespaceMembersAndSelf(this INamespaceSymbol @namespace)
        {
            yield return @namespace;
            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
                yield return nestedNamespace;
        }

        public static IEnumerable<INamespaceSymbol> GetAllContainingNamespaces(this INamespaceSymbol @namespace)
        {
            var containingNamespace = @namespace.ContainingNamespace;
            while (containingNamespace != null)
            {
                yield return containingNamespace;
                containingNamespace = containingNamespace.ContainingNamespace;
            }
        }

        public static IEnumerable<INamedTypeSymbol> GetNestedTypes(this INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nestedType in type.GetTypeMembers().SelectMany(GetNestedTypes))
                yield return nestedType;
        }

        public static IMethodSymbol ReducedFromOrItself(this IMethodSymbol methodSymbol) => methodSymbol.ReducedFrom ?? methodSymbol;
    }
}
