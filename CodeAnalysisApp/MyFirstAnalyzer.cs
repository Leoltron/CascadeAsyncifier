using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private const string ATTR_NAME = "TestConsoleApplication.ExpectConfigureAwaitTrueAttribute";
        private int errorsTotal = 0;
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
/*
            if (attributeSymbol == null)
            {
                Console.WriteLine(
                    $"Did not find {ATTR_NAME} in assembly, proceeding assuming we want .ConfigureAwait(false) everywhere");
            }*/
/*
            var descendantNodes = root.DescendantNodes().ToList();

            descendantNodes.OfType<ClassDeclarationSyntax>()
                .Cast<MemberDeclarationSyntax>()
                .Concat(descendantNodes.OfType<MethodDeclarationSyntax>())
                .ForEach(Analyze);*/
            Analyze(document.Name, root, false);
        }

        public void Analyze(MemberDeclarationSyntax node)
        {
            var expectConfAwaitTrue = attributeSymbol != null &&
                                      node.AttributeLists.SelectMany(a => a.Attributes)
                                          .Select(a => ModelExtensions.GetTypeInfo(model, a).Type)
                                          .Any(a => SymbolEqualityComparer.Default.Equals(attributeSymbol, a));
            //Console.WriteLine($"{expectConfAwaitTrue.ToString().ToUpper()} {node}");
            AnalyzeDescendants(node, expectConfAwaitTrue);
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
                    case ClassDeclarationSyntax _:
                    case MethodDeclarationSyntax _:
                    {
                        var node = childNode as MemberDeclarationSyntax;
                        //Console.WriteLine(childNode.ToString());
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
            var expressionTypeInfo = ModelExtensions.GetTypeInfo(model, awaitNode.Expression);
            var actualArg = true;
            if (expressionTypeInfo.Type.ToDisplayString() != "System.Threading.Tasks.Task")
                foreach (var descNode in awaitNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var a = ModelExtensions.GetSymbolInfo(model, descNode.Expression);

                    if (a.Symbol?.ToDisplayString() != "System.Threading.Tasks.Task.ConfigureAwait(bool)")
                        continue;

                    if (descNode.ArgumentList.Arguments.First().Expression.Kind() != SyntaxKind.FalseLiteralExpression)
                        continue;

                    actualArg = false;

                    break;
                }

            var span = awaitNode.SyntaxTree.GetLineSpan(awaitNode.Span);
            if (actualArg != expectedConfigureAwaitArgument)
            {
                dictionary.AddOrCreate(docName)
                    .Add(
                        (span.StartLinePosition, expectedConfigureAwaitArgument,
                            awaitNode.ToFullString().TrimStart('\n', ' ')));
                errorsTotal++;
            }
        }

        public void AnalyzeDescendants(SyntaxNode syntaxNode, bool expectedConfigureAwaitArgument)
        {
            foreach (var awaitNode in syntaxNode.DescendantNodes().OfType<AwaitExpressionSyntax>())
            {
                var descNodes = awaitNode.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var descNode in descNodes)
                {
                    var a = ModelExtensions.GetSymbolInfo(model, descNode.Expression);
                    var name = a.Symbol.Name;
                    if (a.Symbol.ToDisplayString() == "System.Threading.Tasks.Task.ConfigureAwait(bool)")
                    {
                        var confAwaitTrue = descNode.ArgumentList.Arguments.First().Expression.Kind() ==
                                            SyntaxKind.TrueLiteralExpression;
                        var span = descNode.SyntaxTree.GetLineSpan(descNode.Span);
                        if (confAwaitTrue != expectedConfigureAwaitArgument)
                        {
                            Console.WriteLine(span.StartLinePosition + " " + descNode.ToFullString());
                        }
                        else
                        {
                            Console.WriteLine(span.StartLinePosition + " OK " + descNode.ToFullString());
                        }

                        break;
                    }
                }
            }
        }
    }
}
