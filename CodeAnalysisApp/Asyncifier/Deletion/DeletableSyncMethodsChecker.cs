using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CodeAnalysisApp.Extensions;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Asyncifier.Deletion
{
    public class DeletableSyncMethodsChecker
    {
        /// <summary>
        /// If method's type extends one of listed classes, any sync method which has an async overload can be deleted
        /// </summary>
        private static readonly string[] deletableContainingTypeNames = {
            "System.Web.Mvc.Controller",
            "System.Web.Http.ApiController",
        };
        
        private static readonly ConditionalWeakTable<Compilation, DeletableSyncMethodsChecker> instances = new();


        public static DeletableSyncMethodsChecker GetInstance(Compilation compilation)
        {
            if (instances.TryGetValue(compilation, out var instance))
                return instance;

            instance = new DeletableSyncMethodsChecker(compilation);
            instances.Add(compilation, instance);

            return instance;
        }

        private readonly HashSet<ITypeSymbol> deletableContainingTypes = new(SymbolEqualityComparer.Default);

        private DeletableSyncMethodsChecker(Compilation compilation)
        {
            foreach (var typeName in deletableContainingTypeNames)
            {
                var type = compilation.GetTypeByMetadataName(typeName);
                if (type != null)
                {
                    deletableContainingTypes.Add(type);
                }
            }
        }
        
        public bool CanDeleteSyncMethodWithAsyncOverload(IMethodSymbol syncMethodSymbol)
        {
            return syncMethodSymbol.DeclaredAccessibility == Accessibility.Public && deletableContainingTypes.Any(t => syncMethodSymbol.ContainingType.InheritsFromOrEquals(t));
        }
    }
}
