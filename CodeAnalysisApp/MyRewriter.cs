using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp
{
    public class MyRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel model;
        private readonly INamedTypeSymbol attributeSymbol;
        private const string ATTR_NAME = "TestConsoleApplication.ExpectConfigureAwaitTrueAttribute";

        public MyRewriter(SemanticModel model)
        {
            this.model = model;
            attributeSymbol = model.Compilation.GetTypeByMetadataName(ATTR_NAME);
        }

        private bool expectedConfigureAwaitArgument = false;

        private static bool HasParent(SyntaxNode node, SyntaxNode expectedParent)
        {
            while (true)
            {
                if (node == expectedParent)
                    return true;

                if (node == null)
                    return false;

                node = node.Parent;
            }
        }

        public override SyntaxNode VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            var expressionTypeInfo = ModelExtensions.GetTypeInfo(model, node.Expression);

            if (expressionTypeInfo.Type.ToDisplayString() == typeof(Task).FullName)
            {
                if (expectedConfigureAwaitArgument != true)
                {
                    return InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            node.Expression,
                            IdentifierName("ConfigureAwait")),
                        ArgumentListWithOneBoolArgument(false))
                        .WithTriviaFrom(node);
                }
            }


            var nodeToReturn = base.VisitAwaitExpression(node);

            return nodeToReturn;
        }

        private static ArgumentListSyntax ArgumentListWithOneBoolArgument(bool arg) =>
            ArgumentList()
                .AddArguments(
                    Argument(
                        LiteralExpression(arg ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)));


        private bool? DetermineConfigureAwaitArgument(AwaitExpressionSyntax awaitNode)
        {
            var expressionTypeInfo = ModelExtensions.GetTypeInfo(model, awaitNode.Expression);

            if (expressionTypeInfo.Type.ToDisplayString() == typeof(Task).FullName)
                return true;

            foreach (var descNode in awaitNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var a = ModelExtensions.GetSymbolInfo(model, descNode.Expression);

                if (a.Symbol?.ToDisplayString() != "System.Threading.Tasks.Task.ConfigureAwait(bool)")
                    continue;

                return descNode.ArgumentList.Arguments.First().Expression.Kind() == SyntaxKind.TrueLiteralExpression;
            }

            return null;
        }

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

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
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

            if (argument != expectedConfigureAwaitArgument)
            {
                return InvocationExpression(
                    node.Expression,
                    ArgumentListWithOneBoolArgument(expectedConfigureAwaitArgument));
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
