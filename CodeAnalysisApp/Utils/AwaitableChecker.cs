using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Utils
{
    public class AwaitableChecker
    {
        private readonly INamedTypeSymbol awaitableSymbol;
        private readonly INamedTypeSymbol genericAwaitableSymbol;
        private readonly INamedTypeSymbol genericTaskSymbol;
        private readonly INamedTypeSymbol taskSymbol;

        public AwaitableChecker(Compilation compilation)
        {
            taskSymbol = compilation.GetTypeByMetadataName(typeof(Task).FullName!);
            genericTaskSymbol = compilation.GetTypeByMetadataName(typeof(Task<>).FullName!);
            awaitableSymbol = compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable).FullName!);
            genericAwaitableSymbol =
                compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable<>).FullName!);
        }
        
        public bool IsAwaitable(ITypeSymbol typeSymbol) =>
            typeSymbol.SymbolEquals(awaitableSymbol) ||
            typeSymbol.OriginalDefinition.SymbolEquals(genericAwaitableSymbol) ||
            IsTask(typeSymbol) ||
            IsGenericTask(typeSymbol);

        public bool IsTask(ITypeSymbol typeSymbol) => typeSymbol.SymbolEquals(taskSymbol);
        public bool IsGenericTask(ITypeSymbol typeSymbol) => typeSymbol.OriginalDefinition.SymbolEquals(genericTaskSymbol);
    }
}
