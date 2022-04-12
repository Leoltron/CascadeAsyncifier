using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CascadeAsyncifier.Rewriters;
using CascadeAsyncifier.Extensions;
using CommandLine;
using Serilog;

namespace CascadeAsyncifier
{
    internal static class Program
    {
        private static Task Main(string[] args) =>
            Parser.Default
                  .ParseArguments<CommandLineOptions>(args)
                  .WithParsedAsync(MainWithOptions);

        private static async Task MainWithOptions(CommandLineOptions options)
        {
            SetupLog(options.SolutionPath);

            if (options.SolutionPath.IsNullOrEmpty())
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                Log.Information("No solution specified, searching current directory {Dir}", currentDirectory);
                options.SolutionPath = Directory.GetFiles(currentDirectory, "*.sln", SearchOption.TopDirectoryOnly)
                                                .FirstOrDefault();
                if (options.SolutionPath.IsNullOrEmpty())
                {
                    Log.Error("No file found, aborting");
                    return;
                }
            }

            if (!TryRegisterMsBuild(options.MsBuildPath))
                return;

            var solutionPath = options.SolutionPath;

            using var workspace =
                MSBuildWorkspace.Create(options.TargetFramework.IsNullOrEmpty()
                                            ? ImmutableDictionary<string, string>.Empty
                                            : ImmutableDictionary<string, string>.Empty.Add(
                                                "TargetFramework", options.TargetFramework));

            // Print message for WorkspaceFailed event to help diagnosing project load failures.
            workspace.WorkspaceFailed += (o, e) => Log.Warning(e.Diagnostic.Message);

            Log.Information("Loading solution '{SolutionPath}'", solutionPath);

            // Attach progress reporter so we print projects as they are loaded.
            var solution = await workspace.OpenSolutionAsync(solutionPath, new ConsoleProgressReporter());

            if (solution.Projects.GroupBy(e => e.FilePath).Any(c => c.Count() > 1))
            {
                Log.Error("Loaded multiple projects from same file. This is probably due to targeting multiple frameworks");
                return;
            }

            Log.Information("Finished loading solution '{SolutionPath}'", solutionPath);

            await Rewrite(workspace, options);
        }
        private static async Task Rewrite(MSBuildWorkspace workspace, CommandLineOptions options)
        {
            var rewriters = RewritersInfo.GetRewriters(options).ToArray();
            
            var solutionTraverser = new MutableSolutionTraverser(workspace);

            Log.Information("Applying async void rewriter before asyncification");
            await solutionTraverser.ApplyRewriterAsync(_ => new AsyncVoidRewriter());
            Log.Information("Done");
            
            await new Asyncifier.CascadeAsyncifier(options.StartingFilePathRegex).Start(workspace);
            
            foreach (var factory in rewriters)
            {
                Log.Information("Applying rewriter {RewriterName}", factory.Name);
                await solutionTraverser.ApplyRewriterAsync(factory.Build);
                Log.Information("Rewriter {RewriterName} has finished", factory.Name);
            }
        }

        private static bool TryRegisterMsBuild(string msBuildPath)
        {
            if (!msBuildPath.IsNullOrEmpty())
            {
                MSBuildLocator.RegisterMSBuildPath(msBuildPath);
                return true;
            }
            
            // Attempt to set the version of MSBuild.
            var visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances().ToArray();

            if (visualStudioInstances.Length == 0)
            {
                Log.Error("Failed to automatically detect MSBuild instance. Please specify MSBuild folder through --msbuild-path parameter");
                return false;
            }
            
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
            return true;
        }

        private static VisualStudioInstance SelectVisualStudioInstance(VisualStudioInstance[] visualStudioInstances)
        {
            Console.WriteLine("Multiple installs of MSBuild detected please select one:");
            for (var i = 0; i < visualStudioInstances.Length; i++)
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
            var solutionFileNameSuffix = solutionPath.IsNullOrEmpty() ? "" : "_" + Path.GetFileName(solutionPath);

            var loggerConfiguration = new LoggerConfiguration().WriteTo.Console();
            if (!solutionPath.IsNullOrEmpty())
            {
                loggerConfiguration.WriteTo.File($"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{solutionFileNameSuffix}.log");
            }
            
            Log.Logger = loggerConfiguration.CreateLogger();
        }
    }
}
