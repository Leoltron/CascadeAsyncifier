using System;
using System.Threading.Tasks;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeAnalysisApp.Extensions
{
    public static class TraverserExtensions
    {
        public static Task ApplyRewriterAsync(this MutableSolutionTraverser slnTraverser, Func<SemanticModel, CSharpSyntaxRewriter> rewriterFactory) =>
            slnTraverser.TraverseAsync(
                async document =>
                {
                    var root = await document.GetSyntaxRootAsync();
                    var model = await document.GetSemanticModelAsync();
                    var newSource = root;
                    var rewriter = rewriterFactory(model);
                    newSource = rewriter.Visit(newSource);

                    var oldText = (await document.GetSyntaxRootAsync())!.GetText();
                    var newSourceText = newSource!.GetText();

                    if (newSourceText.ContentEquals(oldText))
                        return TraverseResult.Continue;

                    return oldText.ToString().EqualsIgnoreWhitespace(newSourceText.ToString())
                        ? TraverseResult.Continue
                        : TraverseResult.Reload(document.WithSyntaxRoot(newSource));
                });
    }
}
