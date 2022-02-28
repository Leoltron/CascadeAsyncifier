using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeAnalysisApp
{
    public class MyFirstAnalyzer
    {
        private SemanticModel model;
        private INamedTypeSymbol attributeSymbol;
        private INamedTypeSymbol taskSymbol;
        private INamedTypeSymbol genericTaskSymbol;
        private const string ATTR_NAME = "TestConsoleApplication.KeepSyncContextAttribute";
        private int errorsTotal;
        private readonly Dictionary<string, List<(LinePosition, bool, string)>> dictionary = new();

        public async Task Analyze(Solution solution)
        {
            foreach (var project in solution.Projects)
            {
                await Analyze(project);
            }

            foreach (var d in dictionary)
            {
                Console.WriteLine(d.Key);
                foreach (var (linePosition, expectedArg, line) in d.Value)
                {
                    var message = expectedArg
                        ? "Использован .ConfigureAwait(false) в UI потоке"
                        : "Рекомендуется использовать .ConfigureAwait(false)";
                    Console.WriteLine(linePosition + "\t" + message + "\t" + line);
                }
            }

            Console.WriteLine("Total errors: " + errorsTotal);
        }

        public async Task Analyze(Project project)
        {
            foreach (var document in project.Documents)
            {
                await Analyze(document);
            }
        }

        private async Task Analyze(Document document)
        {
            var root = await document.GetSyntaxRootAsync();

            model = await document.GetSemanticModelAsync();
            attributeSymbol = model.Compilation.GetTypeByMetadataName(ATTR_NAME);
            taskSymbol = model.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
            genericTaskSymbol = model.Compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
            Analyze(document.Name, root, false);
        }

        public void Analyze(string docName, SyntaxNode syntaxNode, bool expectedConfigureAwaitArgument)
        {
            foreach (var childNode in syntaxNode.ChildNodes())
            {
                switch (childNode)
                {
                    case AwaitExpressionSyntax awaitNode:
                        Analyze(docName, awaitNode, expectedConfigureAwaitArgument);

                        break;
                    case ClassDeclarationSyntax:
                    case MethodDeclarationSyntax:
                    {
                        var node = childNode as MemberDeclarationSyntax;
                        var expectConfAwaitTrue =
                            expectedConfigureAwaitArgument || node.HasAttribute(model, attributeSymbol);
                        Analyze(docName, node, expectConfAwaitTrue);

                        break;
                    }
                    case LambdaExpressionSyntax:
                        break;
                    default:
                        Analyze(docName, childNode, expectedConfigureAwaitArgument);

                        break;
                }
            }
        }

        public void Analyze(string docName, AwaitExpressionSyntax awaitNode, bool expectedConfigureAwaitArgument)
        {
            var actualArg = DetermineConfigureAwaitArgument(awaitNode);

            var span = awaitNode.SyntaxTree.GetLineSpan(awaitNode.Span);
            if (actualArg.HasValue && actualArg != expectedConfigureAwaitArgument)
            {
                dictionary.GetOrCreate(docName)
                    .Add(
                        (span.StartLinePosition, expectedConfigureAwaitArgument,
                            awaitNode.ToFullString().TrimStart('\n', ' ')));
                errorsTotal++;
            }
        }

        private bool? DetermineConfigureAwaitArgument(AwaitExpressionSyntax awaitNode)
        {
            var expressionTypeInfo = ModelExtensions.GetTypeInfo(model, awaitNode.Expression);

            if (expressionTypeInfo.Type.SymbolEquals(taskSymbol) ||
                expressionTypeInfo.Type.OriginalDefinition.SymbolEquals(genericTaskSymbol))
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
    }
}
