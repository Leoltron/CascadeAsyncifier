using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Utils
{
    public class AwaitableChecker
    {
        private readonly SemanticModel semanticModel;
        private readonly INamedTypeSymbol awaitableSymbol;
        private readonly INamedTypeSymbol genericAwaitableSymbol;
        private readonly INamedTypeSymbol genericTaskSymbol;
        private readonly INamedTypeSymbol taskSymbol;

        public AwaitableChecker(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
            taskSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
            genericTaskSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
            awaitableSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable).FullName);
            genericAwaitableSymbol =
                semanticModel.Compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable<>).FullName);
        }

        public bool IsTypeAwaitable(SyntaxNode node)
        {
            return IsAwaitable(semanticModel.GetTypeInfo(node).Type);
        }

        public bool IsAwaitable(ITypeSymbol typeSymbol)
        {
            return typeSymbol.SymbolEquals(awaitableSymbol) ||
                   typeSymbol.OriginalDefinition.SymbolEquals(genericAwaitableSymbol) ||
                   IsTask(typeSymbol) ||
                   IsGenericTask(typeSymbol);
        }

        public bool IsTask(ITypeSymbol typeSymbol) => typeSymbol.SymbolEquals(taskSymbol);

        public bool IsGenericTask(ITypeSymbol typeSymbol) => typeSymbol.OriginalDefinition.SymbolEquals(genericTaskSymbol);
    }
}
