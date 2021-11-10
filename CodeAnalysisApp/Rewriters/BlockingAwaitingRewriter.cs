using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp.Rewriters
{
    public class BlockingAwaitingRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel semanticModel;
        private readonly INamedTypeSymbol awaitableSymbol;
        private readonly INamedTypeSymbol genericAwaitableSymbol;
        private readonly INamedTypeSymbol genericTaskSymbol;
        private readonly INamedTypeSymbol taskSymbol;

        public BlockingAwaitingRewriter(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
            taskSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
            genericTaskSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
            awaitableSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable).FullName);
            genericAwaitableSymbol =
                semanticModel.Compilation.GetTypeByMetadataName(typeof(ConfiguredTaskAwaitable<>).FullName);
        }


        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (!InAsyncMethod)
                return base.VisitMemberAccessExpression(node);

            var expType = semanticModel.GetTypeInfo(node.Expression);

            if (node.Name.Identifier.Text == "Result" &&
                expType.Type.OriginalDefinition.SymbolEquals(genericTaskSymbol))
                return AwaitExpression(node.Expression).NormalizeWhitespace();

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!InAsyncMethod || node.Expression is not MemberAccessExpressionSyntax memberAccessNode)
                return base.VisitInvocationExpression(node);


            switch (memberAccessNode.Name.Identifier.Text)
            {
                case "Wait":
                {
                    var expType = semanticModel.GetTypeInfo(memberAccessNode.Expression);

                    if (expType.Type.SymbolEquals(taskSymbol))
                        return AwaitExpression(memberAccessNode.Expression)
                            .NormalizeWhitespace()
                            .WithLeadingTrivia(node.GetLeadingTrivia());

                    break;
                }
                case "GetResult":
                {
                    if (memberAccessNode.Expression is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax innerMemberAccessNode
                    } && innerMemberAccessNode.Name.Identifier.Text == "GetAwaiter")
                    {
                        var expType = semanticModel.GetTypeInfo(innerMemberAccessNode.Expression).Type;

                        if (expType.SymbolEquals(awaitableSymbol) ||
                            expType.OriginalDefinition.SymbolEquals(genericAwaitableSymbol) ||
                            expType.OriginalDefinition.SymbolEquals(genericTaskSymbol) ||
                            expType.SymbolEquals(taskSymbol))
                        {
                            return AwaitExpression(innerMemberAccessNode.Expression)
                                .NormalizeWhitespace()
                                .WithLeadingTrivia(node.GetLeadingTrivia());
                        }
                    }

                    break;
                }
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
