using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Utils
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
            IsAnyTask(typeSymbol);

        public bool IsAnyTask(ITypeSymbol typeSymbol) => IsTask(typeSymbol) || IsGenericTask(typeSymbol);
        public bool IsTask(ITypeSymbol typeSymbol) => typeSymbol.SymbolEquals(taskSymbol);
        public bool IsGenericTask(ITypeSymbol typeSymbol) => typeSymbol.OriginalDefinition.SymbolEquals(genericTaskSymbol);
    }
}
