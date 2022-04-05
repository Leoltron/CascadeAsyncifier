using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Utils
{
    public class AwaitableSyntaxChecker : AwaitableChecker
    {
        private readonly SemanticModel semanticModel;

        public AwaitableSyntaxChecker(SemanticModel semanticModel) : base(semanticModel.Compilation)
        {
            this.semanticModel = semanticModel;
        }

        public bool IsTypeAwaitable(SyntaxNode node) => IsAwaitable(semanticModel.GetTypeInfo(node).Type);
    }
}
