using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class UnawaitedInAsyncMethodCallRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel semanticModel;
        private readonly INamedTypeSymbol awaitableSymbol;
        private readonly INamedTypeSymbol genericAwaitableSymbol;
        private readonly INamedTypeSymbol genericTaskSymbol;
        private readonly INamedTypeSymbol taskSymbol;

        public UnawaitedInAsyncMethodCallRewriter(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
            taskSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
            genericTaskSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
            awaitableSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable).FullName);
            genericAwaitableSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable<>).FullName);
        }

        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (!InAsyncMethod)
                return base.VisitExpressionStatement(node);

            var expType = ModelExtensions.GetTypeInfo(semanticModel, node.Expression).Type;
            
            var isAwaitableExpression = expType.SymbolEquals(taskSymbol) || 
                    expType.SymbolEquals(awaitableSymbol) ||
                    expType.OriginalDefinition.SymbolEquals(genericAwaitableSymbol) ||
                    expType.OriginalDefinition.SymbolEquals(genericTaskSymbol);

            if (!isAwaitableExpression)
                return base.VisitExpressionStatement(node);

            var originalLeadTrivia = node.GetLeadingTrivia();
            
            return base.VisitExpressionStatement(node.WithExpression(SyntaxFactory.AwaitExpression(node.Expression).NormalizeWhitespace()).WithLeadingTrivia(originalLeadTrivia));
        }
    }
}
