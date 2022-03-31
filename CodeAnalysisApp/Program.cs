using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Helpers;
using CodeAnalysisApp.Rewriters;
using CodeAnalysisApp.Utils;
using Serilog;

namespace CodeAnalysisApp
{
    class Program
    {
        private static readonly IList<(Func<SemanticModel, CSharpSyntaxRewriter> factory, string name)>
            rewriterFactories =
                new (Func<SemanticModel, CSharpSyntaxRewriter> factory, string name)[]
                {
                    //(m => new UseAsyncMethodRewriter(m), "Use async method"),
                    (_ => new AsyncVoidRewriter(), "async void"),
                    (m => new UnawaitedInAsyncMethodCallRewriter(m), "async call without \"await\""),
                    (m => new BlockingAwaitingRewriter(m), "blocking awaiting"),
                   // (m => new ConfigureAwaitRewriter(m), "ConfigureAwait()"),
                   (m => new OnlyAwaitInReturnAsyncMethodRewriter(m), "Only one await in return"),
                   (m => new OnlyAwaitInAsyncLambdaRewriter(m), "One statement in lambda"),
                   (m => new AsyncMethodEndsWithAwaitExpressionRewriter(m), "Only one await at the end of method"),
                };

        static async Task Main(string[] args)
        {
            var solutionPath = args[0];
            
            SetupLog(solutionPath);

            RegisterVSMSBuild();

            using var workspace = MSBuildWorkspace.Create();

            // Print message for WorkspaceFailed event to help diagnosing project load failures.
            workspace.WorkspaceFailed += (o, e) => Log.Warning(e.Diagnostic.Message);

            Log.Information("Loading solution '{SolutionPath}'", solutionPath);

            // Attach progress reporter so we print projects as they are loaded.
            var solution = await workspace.OpenSolutionAsync(solutionPath, new ConsoleProgressReporter());
            Log.Information("Finished loading solution '{SolutionPath}'", solutionPath);

            var time = await Rewrite(workspace);

            Log.Information("Rewriters total time:");
            for (var i = 0; i < rewriterFactories.Count; i++)
            {
                Log.Information("\t{Rewriter}:\t {Time}", rewriterFactories[i].name, time[i]);
            }
        }


        private static string CurrentTraverserName = "";
        private static double CurrentTraverserProgress = 0;
        private static bool CurrentTraverserIsFinished => CurrentTraverserProgress == 1;

        private static async Task<TimeSpan[]> Rewrite(MSBuildWorkspace workspace)
        {
            var time = new TimeSpan[rewriterFactories.Count];
            
            var solutionTraverser = new MutableSolutionTraverser(workspace);
            solutionTraverser.ReportProgress += (i, total) =>
            {
                if(CurrentTraverserIsFinished)
                    return;
                CurrentTraverserProgress = Math.Max((double)i/total, CurrentTraverserProgress);
                Console.Write($"\r[{CurrentTraverserName}] {CurrentTraverserProgress:P} ");
                if (CurrentTraverserIsFinished)
                {
                    Console.WriteLine("Done.");
                }
            };

            CurrentTraverserName = "Initial async void";
            await solutionTraverser.ApplyRewriterAsync(_ => new AsyncVoidRewriter());
            
            await new CascadeAsyncifier().Start(workspace);
            
            for (var i = 0; i < rewriterFactories.Count; i++)
            {
                var sw = Stopwatch.StartNew();
                var (factory, name) = rewriterFactories[i];
                CurrentTraverserName = name;
                CurrentTraverserProgress = 0;
                await solutionTraverser.ApplyRewriterAsync(factory);
                time[i] += sw.Elapsed;
            }

            return time;
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

            Log.Information("Using MSBuild at '{Path}' to load projects", instance.MSBuildPath);

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

                Log.Information($"{loadProgress.Operation,-15} {loadProgress.ElapsedTime,-15:m\\:ss\\.fffffff} {projectDisplay}");
            }
        }

        private static void SetupLog(string solutionPath)
        {
            var solutionFileName = Path.GetFileName(solutionPath);

            var loggerConfiguration = new LoggerConfiguration().WriteTo.Console();
            if (!solutionPath.IsNullOrEmpty())
            {
                loggerConfiguration.WriteTo.File($"{DateTime.Now:yyyy-MM-dd_HH-mm-ss_}{solutionFileName}.log");
            }
            
            Log.Logger = loggerConfiguration.CreateLogger();
        }
    }
}
