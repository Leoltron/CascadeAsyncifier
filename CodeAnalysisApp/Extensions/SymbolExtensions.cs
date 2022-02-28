using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Extensions
{
    public static class SymbolExtensions
    {
        public static bool SymbolEquals(this ISymbol one, ISymbol other) => SymbolEqualityComparer.Default.Equals(one, other);

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
    }
}
