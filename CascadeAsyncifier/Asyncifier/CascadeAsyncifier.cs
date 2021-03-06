using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CascadeAsyncifier.Asyncifier.Deletion;
using CascadeAsyncifier.Asyncifier.Matchers;
using CascadeAsyncifier.Helpers;
using CascadeAsyncifier.Rewriters;
using CascadeAsyncifier.Utils;
using CascadeAsyncifier.Visitors;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Serilog;

namespace CascadeAsyncifier.Asyncifier
{
    public class CascadeAsyncifier
    {
        private readonly DocumentFilter documentFilter = new();

        private readonly ConcurrentDictionary<IMethodSymbol, bool> blacklistedMethods =
            new(SymbolEqualityComparer.Default);

        private readonly Regex startingFilePathRegex;

        public CascadeAsyncifier(string startingFilePathRegex)
        {
            this.startingFilePathRegex = startingFilePathRegex.IsNullOrEmpty() ? null :new Regex(startingFilePathRegex);
        }

        public async Task Start(Workspace workspace)
        {
            var sw = Stopwatch.StartNew();
            Log.Information("Asyncifiaction started");
            Log.Information("Collecting initial methods with async overload... ");
            var solution = workspace.CurrentSolution;
            var matchers = await InitAsyncMatchers(solution);
            Log.Information("Done");

            var docTypeMethodHierarchy = await CollectAsyncifiableMethods(solution, matchers);
            await AddAsyncOverloads(workspace, docTypeMethodHierarchy);
            await ApplyUseAsyncMethodRewriter(workspace);
            var asyncifiedMethodsCleaner = new AsyncifiedMethodsCleaner(workspace);
            await asyncifiedMethodsCleaner.DeleteUnusedAsyncifiedMethodsAsync(docTypeMethodHierarchy);
            sw.Stop();
            Log.Information("Asyncifiaction finished in {Time}", sw.Elapsed);
        }

        private async Task<DocTypeMethodHierarchy> CollectAsyncifiableMethods(
            Solution solution, Dictionary<ProjectId, AsyncOverloadMatcher> matchers)
        {
            var docTasks = solution.Projects
                                   .SelectMany(
                                        p => p.Documents
                                              .Where(d => startingFilePathRegex == null || d.FilePath != null && startingFilePathRegex.IsMatch(d.FilePath))
                                              .Where(d => !documentFilter.IgnoreDocument(d))
                                              .Select(
                                                   document => (
                                                       document,
                                                       task: TraverseDocument(document, matchers[p.Id]))))
                                   .ToList();


            Log.Information("Total documents: {TotalDocs}", docTasks.Count);
            Log.Information("Collecting initial asyncifiable methods... ");

            await Task.WhenAll(docTasks.Select(p => p.task));
            Log.Information("Done");
            Log.Information(
                "Looking for asyncifiable methods through initial asyncifiable methods usage and hierarchy... ");

            var documentToClasses = docTasks.ToDictionary(
                docTask => docTask.document,
                d => d.task.Result.Keys.ToList());
            var classToMethods = docTasks.SelectMany(d => d.task.Result).ToDictionary(t => t.Key, t => t.Value);

            var asyncifableMethodsQueue = new Queue<IMethodSymbol>(
                docTasks.SelectMany(d => d.task.Result.Values.SelectMany(l => l.Select(mp => mp.Symbol))));
            var visitedMethods = new HashSet<IMethodSymbol>(asyncifableMethodsQueue.Select(m => m.OriginalDefinition),
                                                            SymbolEqualityComparer.Default);

            while (asyncifableMethodsQueue.Any())
            {
                var methodSymbol = asyncifableMethodsQueue.Dequeue();

                if (methodSymbol.IsAbstract || methodSymbol.IsVirtual)
                {
                    foreach (var method in (await solution.FindOverridesAndImplementationsAsync(methodSymbol))
                            .OfType<IMethodSymbol>())
                    {
                        await AddMethod(method);
                    }
                }
                else
                {
                    var overridenSymbol = methodSymbol.FindOverridenOrImplementedSymbol();

                    if (overridenSymbol != null)
                    {
                        await AddMethod(overridenSymbol);
                    }
                }

                var callers = await SymbolFinder.FindCallersAsync(methodSymbol, solution);
                foreach (var caller in callers.Where(f => f.IsDirect))
                {
                    var document = solution.GetDocument(caller.Locations.Select(e => e.SourceTree).First(e => e != null));
                    var matcher = document != null ? matchers.GetValueOrDefault(document.Project.Id) : null;
                    if (AsyncOverloadCanBeAppliedToCall(caller, methodSymbol.Name, matcher))
                        await AddMethod((IMethodSymbol) caller.CallingSymbol);
                }
            }


            async Task AddMethod(IMethodSymbol method)
            {
                if (method.MethodKind != MethodKind.Ordinary)
                {
                    return;
                }

                if (blacklistedMethods.ContainsKey(method))
                {
                    return;
                }

                if (method.DeclaringSyntaxReferences.IsEmpty)
                {
                    return;
                }

                var methodSyntax =
                    (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences.First().GetSyntaxAsync();

                var typeSyntax = methodSyntax.Parent as TypeDeclarationSyntax;

                var document = solution.GetDocument(methodSyntax.SyntaxTree);
                if (document == null)
                {
                    return;
                }

                var semanticModel = await document.GetSemanticModelAsync();

                var awaitChecker = new AwaitableSyntaxChecker(semanticModel);

                if (methodSyntax.IsAsync() || awaitChecker.IsTypeAwaitable(methodSyntax.ReturnType))
                    return;

                if (!visitedMethods.Add(method.OriginalDefinition) || typeSyntax == null)
                    return;

                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    foreach (var similarMethod in SymbolFinder.FindSimilarSymbols(
                                 method.OriginalDefinition, compilation!))
                    {
                        visitedMethods.Add(similarMethod);
                    }
                }

                if (matchers[document.Project.Id].HasAsyncOverload(method))
                {
                    return;
                }

                if (semanticModel!.GetDeclaredSymbol(typeSyntax) is not ITypeSymbol classSymbol)
                {
                    return;
                }

                asyncifableMethodsQueue.Enqueue(method);

                var classPair = new TypeSyntaxSemanticPair(typeSyntax, classSymbol);
                var methodPair = new MethodSyntaxSemanticPair(methodSyntax, method);
                if (!classToMethods.ContainsKey(classPair))
                {
                    documentToClasses.AddToDictList(document, classPair);
                }

                classToMethods.AddToDictList(classPair, methodPair);
            }

            Log.Information("Done");

            var docsToTypes = documentToClasses
                             .Where(v => !documentFilter.IgnoreDocument(v.Key) && v.Value.Any())
                             .ToDictionary();

            Log.Information("Total of {DocsCount} docs, {TypeCount} types and {MethodCount} methods",
                            docsToTypes.Count,
                            classToMethods.Count(c => c.Value.Any()),
                            classToMethods.Sum(d => d.Value.Count));
            return new DocTypeMethodHierarchy(classToMethods, docsToTypes);
        }

        private static async Task AddAsyncOverloads(Workspace workspace, DocTypeMethodHierarchy docTypeMethodHierarchy)
        {
            Log.Information("Duplicating and asyncifing signature of methods...");
            var slnEditor = new SolutionEditor(workspace.CurrentSolution);
            var totalMethods = docTypeMethodHierarchy.TypeToMethods.Sum(d => d.Value.Count);
            var methodsIndex = 0;
            foreach (var (document, pairs) in docTypeMethodHierarchy.DocsToTypes)
            {
                Console.Write($"\r{(double)methodsIndex / totalMethods:P} ");
                methodsIndex++;
                var editor = await slnEditor.GetDocumentEditorAsync(document.Id);
                var root = await document.GetSyntaxRootAsync();

                foreach (var method in pairs.SelectMany(pair => docTypeMethodHierarchy.TypeToMethods[pair]))
                {
                    var containingNamespace = method.Symbol.ContainingNamespace;
                    var useTaskNamespace = containingNamespace != null && containingNamespace
                                                                         .GetNamespaceMembersAndSelf()
                                                                         .Concat(containingNamespace.GetAllContainingNamespaces())
                                                                         .Any(ns => ns.Name == "Task");
                    var asyncMethodNode = method.Node
                                                .WithAsyncSignatureAndName(!method.Symbol.IsAbstract,
                                                                           useTaskNamespace)
                                                .WithoutRegionTrivia();
                    editor.InsertAfter(method.Node, asyncMethodNode.LeadWithLineFeedIfNotPresent());
                }

                editor.ReplaceNode(root, (n, _) => n is CompilationUnitSyntax cu ? cu.WithTasksUsingDirective() : n);
            }

            workspace.TryApplyChanges(slnEditor.GetChangedSolution());
            Console.WriteLine($"\r{(1):P}");
            Log.Information("Done");
        }

        private static async Task ApplyUseAsyncMethodRewriter(Workspace workspace)
        {
            Log.Information("Replacing methods' calls with async overloads");
            var traverser = new MutableSolutionTraverser(workspace);
            await traverser.ApplyRewriterAsync(m => new UseAsyncOverloadRewriter(m));
            Log.Information("Done");
        }

        private static bool AsyncOverloadCanBeAppliedToCall(
            SymbolCallerInfo caller,
            string calledMethodName,
            AsyncOverloadMatcher matcher)
        {
            if (caller.CallingSymbol is not IMethodSymbol callingSymbol)
                return false;

            if (!caller.Locations.Any(location => AsyncOverloadCanBeAppliedToCallLocation(location, calledMethodName)))
                return false;

            return callingSymbol.WholeHierarchyChainIsInSourceCode() || matcher != null && matcher.HasAsyncOverload(callingSymbol.FindOverridenOrImplementedSymbol());
        }

        private static bool AsyncOverloadCanBeAppliedToCallLocation(Location location, string calledMethodName)
        {
            if (location.SourceTree == null)
                return false;

            var syntaxNode = location.SourceTree.GetRoot().FindNode(location.SourceSpan);

            if (syntaxNode.IsInNoAwaitBlock())
                return false;

            var canBeAutoAsyncified = syntaxNode.IsInvocation() && syntaxNode.IsContainingFunctionADeclaredMethod();

            if (!canBeAutoAsyncified)
                LogHelper.ManualAsyncificationRequired(location, calledMethodName);

            return canBeAutoAsyncified;
        }

        private static async Task<Dictionary<ProjectId, AsyncOverloadMatcher>> InitAsyncMatchers(
            Solution solution)
        {
            var matchers = new Dictionary<ProjectId, AsyncOverloadMatcher>();
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                matchers[project.Id] = AsyncOverloadMatcher.GetInstance(compilation);
            }

            return matchers;
        }

        private async Task<Dictionary<TypeSyntaxSemanticPair, List<MethodSyntaxSemanticPair>>> TraverseDocument(
            Document document, AsyncOverloadMatcher matcher)
        {
            var classToMethods = new Dictionary<ClassDeclarationSyntax, List<MethodDeclarationSyntax>>();

            var visitedMethods = new HashSet<MethodDeclarationSyntax>();

            var root = await document.GetSyntaxRootAsync();
            var model = await document.GetSemanticModelAsync();

            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var finder = new AsyncificationCandidateFinder(model, matcher);
            finder.CandidateFound += m =>
            {
                if (m.Parent is not ClassDeclarationSyntax @class)
                    return;

                if (visitedMethods.Add(m))
                    classToMethods.AddToDictList(@class, m);
            };
            finder.CandidateBlacklisted += m =>
            {
                blacklistedMethods.TryAdd((IMethodSymbol)model.GetDeclaredSymbol(m), true);
                if (m.Parent is not ClassDeclarationSyntax @class)
                    return;

                if (!visitedMethods.Add(m) && classToMethods.ContainsKey(@class))
                    classToMethods[@class].Remove(m);
            };

            finder.Visit(root);

            return classToMethods
                  .Select(
                       p => (
                           new TypeSyntaxSemanticPair(
                               p.Key, model.GetDeclaredSymbol(p.Key) as ITypeSymbol),
                           p.Value.Select(
                                 m => new MethodSyntaxSemanticPair(
                                     m, model.GetDeclaredSymbol(m) as IMethodSymbol))
                            .Where(mp => mp.Symbol != null)
                            .ToList()))
                  .Where(p => p.Item1.Symbol != null && p.Item2.Any())
                  .ToDictionary(p => p.Item1, p => p.Item2);
        }
    }
}
