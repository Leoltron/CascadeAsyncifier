using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using CodeAnalysisApp.Helpers;
using CodeAnalysisApp.Rewriters;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp
{
    class Program
    {
        private static readonly IList<(Func<SemanticModel, CSharpSyntaxRewriter> factory, string name)>
            rewriterFactories =
                new (Func<SemanticModel, CSharpSyntaxRewriter> factory, string name)[]
                {
                    (m => new UseAsyncMethodRewriter(m), "Use async method"),
                    (_ => new AsyncVoidRewriter(), "async void"),
                    (m => new UnawaitedInAsyncMethodCallRewriter(m), "async call without \"await\""),
                    (m => new BlockingAwaitingRewriter(m), "blocking awaiting"),
                    (m => new ConfigureAwaitRewriter(m), "ConfigureAwait()"),
                    (_ => new OnlyAwaitInReturnAsyncMethodRewriter(), "Only one await in return"),
                    (_ => new OnlyAwaitInAsyncLambdaRewriter(), "One statement in lambda"),
                    (m => new AsyncMethodEndsWithAwaitExpressionRewriter(m), "Only one await at the end of method"),
                };

        static async Task Main(string[] args)
        {
            Console.WriteLine(typeof(Task).FullName);

            RegisterVSMSBuild();

            using var workspace = MSBuildWorkspace.Create();

            // Print message for WorkspaceFailed event to help diagnosing project load failures.
            workspace.WorkspaceFailed += (o, e) => Console.WriteLine(e.Diagnostic.Message);

            var solutionPath = args[0];
            Console.WriteLine($"Loading solution '{solutionPath}'");

            // Attach progress reporter so we print projects as they are loaded.
            var solution = await workspace.OpenSolutionAsync(solutionPath, new ConsoleProgressReporter());
            Console.WriteLine($"Finished loading solution '{solutionPath}'");

            //await FindMethods(solution);
            /*
            var solutionTraverser = new MutableSolutionTraverser(workspace);
            await solutionTraverser.TraverseAsync(async d =>
            {
                var model = await d.GetSemanticModelAsync();
                var root = await d.GetSyntaxRootAsync();
                root.DescendantNodes().OfType<InvocationExpressionSyntax>().ForEach(invoc =>
                {
                    if (model.GetSymbolInfo(invoc).Symbol is IMethodSymbol symbol && repl.Any(r => r.SymbolEquals(symbol)))
                        Console.WriteLine(d.Name+invoc.Span + " - "+invoc.ToString());
                });
            return TraverseResult.Continue;
            });*/

            
            var time = await Rewrite(workspace);

            Console.WriteLine("Rewriters total time:");
            for (var i = 0; i < rewriterFactories.Count; i++)
            {
                Console.WriteLine("\t" + rewriterFactories[i].name + ":\t " + time[i]);
            }
        }
/*
        private static async Task FindMethods(Solution solution)
        {
            foreach (var project in solution.Projects)
            {
                var a = await project.GetCompilationAsync();
                if (a == null)
                {
                    Console.WriteLine($"Project \"{project.Name}\": no compilation found");
                    continue;
                }

                Console.WriteLine($"Project \"{project.Name}\"");

                var b = new AsyncifiableMethodsProvider(a);
                b.Provide().ToList();
            }
        }*/

        private static async Task<TimeSpan[]> Rewrite(MSBuildWorkspace workspace)
        {
            var time = new TimeSpan[rewriterFactories.Count];

            var solutionTraverser = new MutableSolutionTraverser(workspace);
            await ApplyRewriter(solutionTraverser, _ => new AsyncVoidRewriter());
            
            await new CascadeAsyncifier().Start(workspace);
            
            for (var i = 0; i < rewriterFactories.Count; i++)
            {
                var sw = Stopwatch.StartNew();
                var rewriterAndName = rewriterFactories[i];
                await ApplyRewriter(solutionTraverser, rewriterAndName.factory);
                time[i] += sw.Elapsed;
            }

            return time;
        }

        private static Task ApplyRewriter(MutableSolutionTraverser slnTraverser, Func<SemanticModel, CSharpSyntaxRewriter> rewriterFactory)
        {
            return slnTraverser.TraverseAsync(
                async document =>
                {
                    var root = await document.GetSyntaxRootAsync();
                    var model = await document.GetSemanticModelAsync();
                    var newSource = root;
                    var rewriter = rewriterFactory(model);
                    newSource = rewriter.Visit(newSource);

                    var oldText = (await document.GetSyntaxRootAsync()).GetText();
                    var newSourceText = newSource.GetText();

                    if (newSourceText.ContentEquals(oldText))
                        return TraverseResult.Continue;

                    return oldText.ToString().EqualsIgnoreWhitespace(newSourceText.ToString())
                        ? TraverseResult.Continue
                        : TraverseResult.Reload(document.WithSyntaxRoot(newSource));
                });
        }


        private static void RegisterVSMSBuild()
        {
// Attempt to set the version of MSBuild.
            var visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            var instance = visualStudioInstances.Length == 1
                // If there is only one instance of MSBuild on this machine, set that as the one to use.
                ? visualStudioInstances[0]
                // Handle selecting the version of MSBuild you want to use.
                : SelectVisualStudioInstance(visualStudioInstances);

            Console.WriteLine($"Using MSBuild at '{instance.MSBuildPath}' to load projects.");

            // NOTE: Be sure to register an instance with the MSBuildLocator 
            //       before calling MSBuildWorkspace.Create()
            //       otherwise, MSBuildWorkspace won't MEF compose.
            MSBuildLocator.RegisterInstance(instance);
        }

        private static VisualStudioInstance SelectVisualStudioInstance(VisualStudioInstance[] visualStudioInstances)
        {
            Console.WriteLine("Multiple installs of MSBuild detected please select one:");
            for (int i = 0; i < visualStudioInstances.Length; i++)
            {
                Console.WriteLine($"Instance {i + 1}");
                Console.WriteLine($"    Name: {visualStudioInstances[i].Name}");
                Console.WriteLine($"    Version: {visualStudioInstances[i].Version}");
                Console.WriteLine($"    MSBuild Path: {visualStudioInstances[i].MSBuildPath}");
            }

            return visualStudioInstances.First();

            while (true)
            {
                var userResponse = Console.ReadLine();
                if (int.TryParse(userResponse, out int instanceNumber) &&
                    instanceNumber > 0 &&
                    instanceNumber <= visualStudioInstances.Length)
                {
                    return visualStudioInstances[instanceNumber - 1];
                }

                Console.WriteLine("Input not accepted, try again.");
            }
        }

        private class ConsoleProgressReporter : IProgress<ProjectLoadProgress>
        {
            public void Report(ProjectLoadProgress loadProgress)
            {
                var projectDisplay = Path.GetFileName(loadProgress.FilePath);
                if (loadProgress.TargetFramework != null)
                {
                    projectDisplay += $" ({loadProgress.TargetFramework})";
                }

                Console.WriteLine(
                    $"{loadProgress.Operation,-15} {loadProgress.ElapsedTime,-15:m\\:ss\\.fffffff} {projectDisplay}");
            }
        }
    }
}
