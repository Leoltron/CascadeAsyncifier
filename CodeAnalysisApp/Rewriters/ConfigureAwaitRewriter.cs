using System;
using System.Threading.Tasks;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp.Rewriters
{
    public class ConfigureAwaitRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel model;
        private readonly INamedTypeSymbol attributeSymbol;
        private readonly INamedTypeSymbol taskSymbol;
        private readonly INamedTypeSymbol genericTaskSymbol;
        private const string ATTR_NAME = "TestConsoleApplication.KeepSyncContextAttribute";

        public ConfigureAwaitRewriter(SemanticModel model)
        {
            this.model = model;
            attributeSymbol = model.Compilation.GetTypeByMetadataName(ATTR_NAME);
            taskSymbol = model.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
            genericTaskSymbol = model.Compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
        }

        private bool expectedConfigureAwaitArgument;
        private bool insideAwaitExpression = false;

        public override SyntaxNode VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            var expressionTypeInfo = ModelExtensions.GetTypeInfo(model, node.Expression);

            if (IsTaskCompletedTaskExpression(node.Expression))
            {
                return node;
            }

            if (expressionTypeInfo.Type.SymbolEquals(taskSymbol) ||
                expressionTypeInfo.Type.OriginalDefinition.SymbolEquals( genericTaskSymbol))
            {
                if (expectedConfigureAwaitArgument != true)
                {
                    return node.Update(VisitToken(node.AwaitKeyword),
                                       InvocationExpression(
                                           MemberAccessExpression(
                                               SyntaxKind.SimpleMemberAccessExpression,
                                               node.Expression,
                                               IdentifierName("ConfigureAwait")),
                                           ArgumentListWithOneBoolArgument(false)));
                }
            }


            var oldInsideAwait = insideAwaitExpression;
            insideAwaitExpression = true;
            var nodeToReturn = base.VisitAwaitExpression(node);
            insideAwaitExpression = oldInsideAwait;
            
            return nodeToReturn;
        }

        private bool IsTaskCompletedTaskExpression(ExpressionSyntax expressionSyntax)
        {
            if (expressionSyntax is not MemberAccessExpressionSyntax maes)
                return false;
            var expressionTypeInfo = ModelExtensions.GetTypeInfo(model, maes.Expression);

            return SymbolEqualityComparer.Default.Equals(expressionTypeInfo.Type, taskSymbol) &&
                   maes.Name.Identifier.Text == "CompletedTask";
        }

        private static ArgumentListSyntax ArgumentListWithOneBoolArgument(bool arg) =>
            ArgumentList()
               .AddArguments(
                    Argument(
                        LiteralExpression(arg ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)));


        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) =>
            VisitClassOrMethodExpression(node, () => base.VisitMethodDeclaration(node));

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node) =>
            VisitClassOrMethodExpression(node, () => base.VisitClassDeclaration(node));

        private SyntaxNode VisitClassOrMethodExpression(MemberDeclarationSyntax memberNode, Func<SyntaxNode> baseVisit)
        {
            var oldExpectedArg = expectedConfigureAwaitArgument;

            if (memberNode.HasAttribute(model, attributeSymbol))
                expectedConfigureAwaitArgument = true;

            var nodeToReturn = baseVisit();

            expectedConfigureAwaitArgument = oldExpectedArg;

            return nodeToReturn;
        }

        public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;

        public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!insideAwaitExpression)
                return base.VisitInvocationExpression(node);
            
            var a = ModelExtensions.GetSymbolInfo(model, node.Expression);

            if (a.Symbol?.ToDisplayString() != "System.Threading.Tasks.Task.ConfigureAwait(bool)")
                return base.VisitInvocationExpression(node);

            var configureAwaitArgument = node.ArgumentList.Arguments.First();
            bool argument;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (configureAwaitArgument.Expression.Kind())
            {
                case SyntaxKind.TrueLiteralExpression:
                    argument = true;

                    break;
                case SyntaxKind.FalseLiteralExpression:
                    argument = false;

                    break;
                default:
                    return base.VisitInvocationExpression(node);
            }

            if (argument != expectedConfigureAwaitArgument || argument)
            {
                if (expectedConfigureAwaitArgument && node.Expression is MemberAccessExpressionSyntax expression)
                {
                    return expression.Expression;
                }
                
                return InvocationExpression(
                    node.Expression,
                    ArgumentListWithOneBoolArgument(expectedConfigureAwaitArgument));
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
