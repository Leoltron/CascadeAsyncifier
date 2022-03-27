using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Rewriters;
using CodeAnalysisApp.Utils;
using CodeAnalysisApp.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Serilog;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharp.CSharpExtensions;

namespace CodeAnalysisApp.Helpers
{
    public class CascadeAsyncifier
    {
        private readonly DocumentFilter documentFilter = new DocumentFilter();
        private readonly ConcurrentDictionary<IMethodSymbol, bool> blacklistedMethods = new(SymbolEqualityComparer.Default);
        
        public async Task Start(Workspace workspace)
        {
            Log.Information("Asyncifiaction started");
            Log.Information("Collecting initial methods with async overload... ");
            
            var solution = workspace.CurrentSolution;
            var matchers = new Dictionary<ProjectId, AsyncifiableMethodsMatcher>();
            foreach (var project in solution.Projects)
            {
                var matcher = new AsyncifiableMethodsMatcher(await project.GetCompilationAsync());
                matcher.FillAsyncifiableMethodsFromCompilation();
                matchers[project.Id] = matcher;
            }
            
            Log.Information("Done");
            
            var docTasks = solution.Projects
                .SelectMany(p =>
                                    {
                                        var matcher = matchers[p.Id];
                                        return p.Documents.Where(d => !documentFilter.IgnoreDocument(d)).Select(document => (document, task: TraverseDocument(document, matcher)));
                                    })
                .ToList();
            
            
            Log.Information("Total documents: {TotalDocs}", docTasks.Count);
            Log.Information("Collecting initial asyncifiable methods... ");

            await Task.WhenAll(docTasks.Select(p => p.task));
            Log.Information("Done");
            Log.Information("Looking for asyncifiable methods through initial asyncifiable methods usage and hierarchy... ");

            var documentToClasses = docTasks.ToDictionary(
                docTask => docTask.document,
                d => d.task.Result.Keys.ToList());
            var classToMethods = docTasks.SelectMany(d => d.task.Result).ToDictionary(t => t.Key, t => t.Value);

            var asyncifableMethodsQueue = new Queue<IMethodSymbol>(
                docTasks.SelectMany(d => d.task.Result.Values.SelectMany(l => l.Select(mp => mp.Symbol))));
            var visitedMethods = new HashSet<IMethodSymbol>(asyncifableMethodsQueue.Select(m => m.OriginalDefinition), SymbolEqualityComparer.Default);

            while (asyncifableMethodsQueue.Any())
            {
                var methodSymbol = asyncifableMethodsQueue.Dequeue();
                
                if (methodSymbol.IsAbstract || methodSymbol.IsVirtual)
                {
                    foreach (var method in (await solution.FindOverridesAndImplementationsAsync(methodSymbol)).OfType<IMethodSymbol>())
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
                    if(caller.CallingSymbol is IMethodSymbol callingSymbol && 
                       caller.Locations.Any(location =>
                       {
                           if (location.SourceTree == null)
                               return false;

                           var syntaxNode = location.SourceTree.GetRoot().FindNode(location.SourceSpan);

                           if (syntaxNode.IsInNoAwaitBlock())
                           {
                               return false;
                           }

                           var canBeAutoAsyncified = 
                               IsInvocation(syntaxNode)
                               && syntaxNode.IsContainingFunctionADeclaredMethod();
                           
                           if(!canBeAutoAsyncified)
                               LogHelper.ManualAsyncificationRequired(location, methodSymbol.Name);

                           return canBeAutoAsyncified;
                       }) 
                       && callingSymbol.WholeHierarchyChainIsInSourceCode())
                        await AddMethod(callingSymbol);
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
                    foreach (var similarMethod in SymbolFinder.FindSimilarSymbols(method.OriginalDefinition, compilation!))
                    {
                        visitedMethods.Add(similarMethod);
                    }
                }

                if (matchers[document.Project.Id].CanBeAsyncified(method))
                {
                    return;
                }

                if (ModelExtensions.GetDeclaredSymbol(semanticModel!, typeSyntax) is not ITypeSymbol classSymbol)
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

            
            var docClassPairs = documentToClasses.Where(v => !documentFilter.IgnoreDocument(v.Key) && v.Value.Any()).ToList();
            var totalClasses = classToMethods.Count(c => c.Value.Any());
            
            var totalMethods = classToMethods.Sum(d => d.Value.Count);
            var methodsIndex = 0;
            
            Log.Information("Total of {DocsCount} docs, {TypeCount} types and {MethodCount} methods",docClassPairs.Count,totalClasses, totalMethods);
            
            if(totalMethods == 0)
                return;
            
            Log.Information("Duplicating and asyncifing signature of methods...");

            var slnEditor = new SolutionEditor(workspace.CurrentSolution);
            foreach (var docClassPair in docClassPairs)
            {
                Console.Write($"\r{(double)methodsIndex/totalMethods:P} ");
                methodsIndex++;
                var document = docClassPair.Key;
                var editor = await slnEditor.GetDocumentEditorAsync(document.Id);
                var root = await document.GetSyntaxRootAsync();

                foreach (var method in docClassPair.Value.SelectMany(pair => classToMethods[pair]))
                {
                    var asyncMethodNode = method.Node.WithAsyncSignatureAndName(!method.Symbol.IsAbstract).WithoutRegionTrivia();
                    editor.InsertAfter(method.Node, asyncMethodNode.LeadWithLineFeedIfNotPresent());
                }
                editor.ReplaceNode(root, (n, gen) => n is CompilationUnitSyntax cu ? cu.WithTasksUsingDirective() : n);
            }

            workspace.TryApplyChanges(slnEditor.GetChangedSolution());
            Console.WriteLine($"\r{(1):P}");
            Log.Information("Done");

            Log.Information("Replacing methods' calls with async overloads");
            var traverser = new MutableSolutionTraverser(workspace);
            await traverser.ApplyRewriterAsync(m => new UseAsyncMethodRewriter(m));
            Log.Information("Done");


            await DeleteUnusedAsyncifiedMethodsAsync(workspace, docClassPairs, classToMethods);
        }

        private static async Task DeleteUnusedAsyncifiedMethodsAsync(Workspace workspace, 
                                                                List<KeyValuePair<Document, List<TypeSyntaxSemanticPair>>> typesWithAsyncifiedMethods,
                                                                Dictionary<TypeSyntaxSemanticPair, List<MethodSyntaxSemanticPair>> asyncifiedMethods)
        {
            var methodSymbolToSyncAsyncPair =
                new Dictionary<IMethodSymbol, (DocumentId documentId, MethodSyntaxSemanticPair sync, MethodSyntaxSemanticPair
                    async)>(SymbolEqualityComparer.Default);
            var ignoredMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            Log.Information("Collecting generated methods syntax references");
            var solution = workspace.CurrentSolution;
            foreach (var docClassPair in typesWithAsyncifiedMethods)
            {
                var document = solution.GetDocument(docClassPair.Key.Id);
                var root = await document!.GetSyntaxRootAsync();
                var model = await document.GetSemanticModelAsync();

                if (root == null)
                {
                    Log.Error("Failed to retrieve syntax root for document {DocPath}", document.FilePath);
                    continue;
                }

                if (model == null)
                {
                    Log.Error("Failed to retrieve semantic model for document {DocPath}", document.FilePath);
                    continue;
                }

                foreach (var method in docClassPair.Value.SelectMany(pair => asyncifiedMethods[pair]))
                {
                    var methodSyntax = (MethodDeclarationSyntax)root
                                                               .DescendantNodesAndSelf()
                                                               .FirstOrDefault(
                                                                    n => SyntaxFactory.AreEquivalent(method.Node, n));

                    if (methodSyntax == null)
                    {
                        Log.Error("Failed to find syntax node for previously found method {Method} in document {DocPath}",
                                  method.Node.Identifier.Text, document.FilePath);
                        continue;
                    }

                    var methodSymbol = model.GetDeclaredSymbol(methodSyntax);

                    if (methodSymbol == null)
                    {
                        Log.Error("Failed to find a symbol for previously found method {Method} in document {DocPath}",
                                  method.Node.Identifier.Text, document.FilePath);
                        continue;
                    }

                    var usages =
                        (await SymbolFinder.FindCallersAsync(methodSymbol, solution)).Sum(
                            c => c.Locations.Count());

                    var nextSibling = methodSyntax.GetNextSibling();
                    if (nextSibling is not MethodDeclarationSyntax asyncMethodSyntax)
                    {
                        Log.Error(
                            "Expected next sibling of {Method} to be its async version (since its where tool inserted it) in document {DocPath}, found {ActualSibling}",
                            method.Node.Identifier.Text, document.FilePath, nextSibling.GetType().ToString());
                        continue;
                    }

                    var asyncMethodSymbol = model.GetDeclaredSymbol(asyncMethodSyntax);
                    if (asyncMethodSymbol == null)
                    {
                        Log.Error("Failed to find a symbol for {Method} (async overload of {SyncMethod}) in document {DocPath}",
                                  asyncMethodSyntax.Identifier.Text, methodSyntax.Identifier.Text, document.FilePath);
                        continue;
                    }

                    var asyncUsages =
                        (await SymbolFinder.FindCallersAsync(asyncMethodSymbol, solution)).Sum(
                            c => c.Locations.Count());

                    Log.Verbose(
                        "Usage stats for {Sync} - {Usages} usages, for {Async} - {AsyncUsages} usages",
                        methodSymbol.Name,
                        usages,
                        asyncMethodSymbol.Name,
                        asyncUsages);

                    var isImplementationOrOverride = methodSymbol.FindOverridenOrImplementedSymbol() != null;

                    if (isImplementationOrOverride)
                    {
                        ignoredMethods.Add(methodSymbol);
                        ignoredMethods.Add(asyncMethodSymbol);
                    }

                    if (!isImplementationOrOverride && asyncUsages == 0)
                    {
                        if (usages == 0)
                        {
                            Log.Warning(
                                "Method {0} and generated async overload {1} in file {2} have no usages. " +
                                "If {0} is used implicitly, make sure {1} is used instead and consider removing {0}. " +
                                "If its not a part of library API and is not used explicitly, consider removing both {0} and {1} from source code.",
                                methodSymbol.Name,
                                asyncMethodSymbol.Name,
                                document.FilePath);

                            var shouldDelete = PromptUserToDelete(methodSymbol.Name);

                            if (shouldDelete)
                            {
                                methodSymbolToSyncAsyncPair.Add(
                                    methodSymbol,
                                    (document.Id, (methodSyntax, methodSymbol), (asyncMethodSyntax, asyncMethodSymbol)));
                            }
                        }
                        else
                        {
                            Log.Warning(
                                "Async overload of method {0} ({1}) is not used. " +
                                "This means the tool failed to automatically use {1} instead of {0} and user have to manually check {0}'s usages and, if possible, use {1}.",
                                methodSymbol.Name,
                                asyncMethodSymbol.Name);
                        }
                    }
                    else
                    {
                        methodSymbolToSyncAsyncPair.Add(
                            methodSymbol,
                            (document.Id, (methodSyntax, methodSymbol), (asyncMethodSyntax, asyncMethodSymbol)));
                    }
                }
            }


            Log.Information("Scanning usages to determine which methods became obsolete and can be deleted");
            var methodsToDelete = new Dictionary<MethodDeclarationSyntax, DocumentId>();

            async Task AddMethodToDeleteAsync(SyntaxReference reference)
            {
                var document = solution.GetDocument(reference.SyntaxTree);

                if (document == null)
                {
                    Log.Error("Failed to find document for syntax reference in {Path} {Span}", reference.SyntaxTree.FilePath,
                              reference.Span.ToString());
                    return;
                }

                if (await reference.GetSyntaxAsync() is not MethodDeclarationSyntax syntax)
                {
                    Log.Error("Failed to find method declaration syntax for syntax reference in {Path} {Span}",
                              reference.SyntaxTree.FilePath, reference.Span.ToString());
                    return;
                }

                methodsToDelete.Add(syntax, document.Id);
            }

            async Task TryAddMethodImplementationsToDeleteAsync(IMethodSymbol symbol)
            {
                foreach (var method in (await solution.FindOverridesAndImplementationsAsync(symbol)).OfType<IMethodSymbol>())
                {
                    if ((await SymbolFinder.FindCallersAsync(method, solution)).Any())
                        continue;

                    var syntaxReference = method.DeclaringSyntaxReferences.FirstOrDefault();

                    if (syntaxReference == null)
                        continue;

                    await AddMethodToDeleteAsync(syntaxReference);

                    await TryAddMethodImplementationsToDeleteAsync(method);
                }
            }


            int methodsToDeletePrevCount;
            do
            {
                methodsToDeletePrevCount = methodsToDelete.Count;

                foreach (var pair in methodSymbolToSyncAsyncPair.Where(e => !ignoredMethods.Contains(e.Key)).ToList())
                {
                    var (documentId, syncMethod, asyncMethod) = pair.Value;
                    var semanticModel = await solution.GetDocument(documentId).GetSemanticModelAsync();
                    var nunitChecker = new NUnitTestAttributeChecker(semanticModel.Compilation);

                    var syncUsages =
                        (await SymbolFinder.FindCallersAsync(syncMethod.Symbol, solution)).ToList();
                    var syncUsagesCount = syncUsages.Sum(c => c.Locations.Count());

                    if (syncUsagesCount == 0 ||
                        syncUsages.All(u => u.CallingSymbol is IMethodSymbol methodSymbol &&
                                            nunitChecker.HasTestAttribute(methodSymbol)))
                    {
                        methodsToDelete.Add(syncMethod.Node, documentId);
                        foreach (var usage in syncUsages)
                        {
                            await AddMethodToDeleteAsync(usage.CallingSymbol.DeclaringSyntaxReferences.First());
                        }

                        await TryAddMethodImplementationsToDeleteAsync(syncMethod.Symbol);

                        methodSymbolToSyncAsyncPair.Remove(pair.Key);
                    }
                }
            } while (methodsToDeletePrevCount != methodsToDelete.Count);

            if (methodsToDelete.Any())
            {
                Log.Information("{MethodCount} methods to remove, deleting...", methodsToDelete.Count);
                var solutionEditor = new SolutionEditor(solution);
                foreach (var g in methodsToDelete.GroupBy(e => e.Value, p => p.Key))
                {
                    var editor = await solutionEditor.GetDocumentEditorAsync(g.Key);
                    foreach (var syntax in g)
                    {
                        editor.RemoveNode(syntax, SyntaxRemoveOptions.KeepDirectives);
                    }
                }

                workspace.TryApplyChanges(solutionEditor.GetChangedSolution());
                Log.Information("Done");
            }
            else
            {
                Log.Information("No methods to delete");
            }
        }

        private static bool PromptUserToDelete(string methodName)
        {
            while (true)
            {
                Console.Write($"Should the tool delete {methodName}? It might help to automatically detect and delete unused methods that {methodName} used, so prefer this to manual deletion (y/n) ");
                var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                switch (answer)
                {
                    case "y":
                        return true;
                    case "n":
                        return false;
                }
            }
        }

        private async Task<Dictionary<TypeSyntaxSemanticPair, List<MethodSyntaxSemanticPair>>> TraverseDocument(
            Document document, AsyncifiableMethodsMatcher matcher)
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
                /*
                                if (model.GetDeclaredSymbol(m) is not IMethodSymbol methodSymbol)
                                    return;*/

                if (m.Parent is not ClassDeclarationSyntax @class)
                    return;

                if (visitedMethods.Add(m))
                    classToMethods.AddToDictList(@class, m);
            };
            finder.CandidateBlacklisted += m =>
            {
                blacklistedMethods.TryAdd((IMethodSymbol) ModelExtensions.GetDeclaredSymbol(model, m), true);
                if (m.Parent is not ClassDeclarationSyntax @class)
                    return;
                
                if (!visitedMethods.Add(m) && classToMethods.ContainsKey(@class))
                    classToMethods[@class].Remove(m);

            };

            finder.Visit(root);

            return classToMethods
                .Select(
                    p => (new TypeSyntaxSemanticPair(p.Key, ModelExtensions.GetDeclaredSymbol(model, p.Key) as ITypeSymbol),
                        p.Value.Select(
                                m => new MethodSyntaxSemanticPair(m, ModelExtensions.GetDeclaredSymbol(model, m) as IMethodSymbol))
                            .Where(mp => mp.Symbol != null)
                            .ToList()))
                .Where(p => p.Item1.Symbol != null && p.Item2.Any())
                .ToDictionary(p => p.Item1, p => p.Item2);
        }

        private static bool IsInvocation(SyntaxNode node)
        {
            while (node != null)
            {
                switch (node.Kind())
                {
                    case SyntaxKind.Argument:
                    case SyntaxKind.CoalesceAssignmentExpression:
                    case SyntaxKind.SimpleAssignmentExpression:
                        return false;
                    case SyntaxKind.InvocationExpression:
                        return true;
                }

                node = node.Parent;
            }

            return false;
        }
    }
}
