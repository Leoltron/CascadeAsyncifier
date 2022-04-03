using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Helpers.UnusedMethods;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Serilog;

namespace CodeAnalysisApp.Helpers
{
    public class AsyncifiedMethodsCleaner
    {
        private readonly Workspace workspace;

        private Solution Solution => workspace.CurrentSolution;

        private Dictionary<MethodDeclarationSyntax, DocumentId> methodsToDelete;
        private HashSet<IMethodSymbol> methodSymbolsToDelete;
        
        public AsyncifiedMethodsCleaner(Workspace workspace)
        {
            this.workspace = workspace;
        }
        
        public async Task DeleteUnusedAsyncifiedMethodsAsync(DocTypeMethodHierarchy asyncifiedMethodsHierarchy)
        {
            if (asyncifiedMethodsHierarchy.TypeToMethods.All(d => d.Value.Count == 0))
            {
                return;
            }

            Log.Information("Scanning usages to determine which methods became obsolete and can be deleted");

            methodsToDelete = new Dictionary<MethodDeclarationSyntax, DocumentId>();
            methodSymbolsToDelete = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            var methodSymbolToSyncAsyncPair =
                new Dictionary<IMethodSymbol, (DocumentId documentId, MethodSyntaxSemanticPair sync, MethodSyntaxSemanticPair async)>(SymbolEqualityComparer.Default);
            var pairsToPromptDeletion =
                new List<(Document document, MethodSyntaxSemanticPair sync, MethodSyntaxSemanticPair async)>();
            var ignoredMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            Log.Information("Collecting generated methods syntax references");
            foreach (var (oldDocument, types) in asyncifiedMethodsHierarchy.DocsToTypes)
            {
                var document = Solution.GetDocument(oldDocument.Id);
                
                var root = await document!.GetSyntaxRootAsync();
                if (root == null)
                {
                    Log.Error("Failed to retrieve syntax root for document {DocPath}", document.FilePath);
                    continue;
                }

                var model = await document.GetSemanticModelAsync();
                if (model == null)
                {
                    Log.Error("Failed to retrieve semantic model for document {DocPath}", document.FilePath);
                    continue;
                }

                var undeletableMethodChecker = new UndeletableSyncMethodChecker(model.Compilation);
                var deletableMethodChecker = DeletableSyncMethodsChecker.GetInstance(model.Compilation);

                foreach (var method in types.SelectMany(pair => asyncifiedMethodsHierarchy.TypeToMethods[pair]))
                {
                    var methodSyntax = (MethodDeclarationSyntax)
                        root
                           .DescendantNodesAndSelf()
                           .FirstOrDefault(n => SyntaxFactory.AreEquivalent(method.Node, n));
                    if (methodSyntax == null)
                    {
                        Log.Error(
                            "Failed to find syntax node for previously found method {Method} in document {DocPath}",
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

                    if (undeletableMethodChecker.ShouldKeepMethod(methodSymbol))
                    {
                        continue;
                    }

                    if (deletableMethodChecker.CanDeleteSyncMethodWithAsyncOverload(methodSymbol))
                    {
                        await AddMethodToDeleteAsync(methodSymbol, methodSymbol.DeclaringSyntaxReferences.First());
                        continue;
                    }

                    var usagesCount =
                        (await SymbolFinder.FindCallersAsync(methodSymbol, Solution)).Sum(
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
                        Log.Error(
                            "Failed to find a symbol for {Method} (async overload of {SyncMethod}) in document {DocPath}",
                            asyncMethodSyntax.Identifier.Text, methodSyntax.Identifier.Text, document.FilePath);
                        continue;
                    }
                    var asyncUsagesCount =
                        (await SymbolFinder.FindCallersAsync(asyncMethodSymbol, Solution)).Sum(
                            c => c.Locations.Count());

                    Log.Verbose(
                        "Usage stats for {Sync} - {Usages} usages, for {Async} - {AsyncUsages} usages",
                        methodSymbol.Name,
                        usagesCount,
                        asyncMethodSymbol.Name,
                        asyncUsagesCount);

                    var isImplementationOrOverride = methodSymbol.FindOverridenOrImplementedSymbol() != null;
                    if (isImplementationOrOverride)
                    {
                        ignoredMethods.Add(methodSymbol);
                        ignoredMethods.Add(asyncMethodSymbol);
                    }

                    if (!isImplementationOrOverride && asyncUsagesCount == 0)
                    {
                        if (usagesCount == 0)
                        {
                            pairsToPromptDeletion.Add(
                                (document, (methodSyntax, methodSymbol), (asyncMethodSyntax, asyncMethodSymbol)));
                        }
                        else
                        {
                            Log.Warning(
                                "Async overload of method {2}.{0} ({1}) is not used. " +
                                "This means the tool failed to automatically use {1} instead of {0} and user have to manually check {0}'s usages and, if possible, use {1}.",
                                methodSymbol.Name,
                                asyncMethodSymbol.Name,
                                methodSymbol.ContainingType.Name);
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

            if (pairsToPromptDeletion.Any())
            {
                Log.Warning(
                    "Some of methods are not used along with their new async overloads. Prompting user to decide their fate");
                foreach (var (document, syncPair, asyncPair) in pairsToPromptDeletion)
                {
                    Log.Warning("Prompting about {Method} in {Path}", syncPair.Symbol.Name, document.FilePath);
                    
                    var shouldDelete = PromptUserToDelete(syncPair.Symbol.Name);
                    if (shouldDelete)
                    {
                        Log.Warning("{Path} - {Method} will be deleted", document.FilePath, syncPair.Symbol.Name);
                        methodSymbolToSyncAsyncPair.Add(syncPair.Symbol, (document.Id, syncPair, asyncPair));
                    }
                    else
                    {
                        Log.Warning("{Path} - {Method} is ignored", document.FilePath, syncPair.Symbol.Name);
                    }
                }
            }


            int methodsToDeletePrevCount;
            do
            {
                methodsToDeletePrevCount = methodsToDelete.Count;

                foreach (var pair in methodSymbolToSyncAsyncPair.Where(e => !ignoredMethods.Contains(e.Key)).ToList())
                {
                    var (documentId, syncMethod, _) = pair.Value;
                    var semanticModel = await Solution.GetDocument(documentId).GetSemanticModelAsync();
                    var nunitChecker = TestAttributeChecker.GetInstance(semanticModel.Compilation);

                    var syncUsages =
                        (await SymbolFinder.FindCallersAsync(syncMethod.Symbol, Solution)).ToList();

                    if (syncUsages.All(u => u.CallingSymbol is IMethodSymbol methodSymbol &&
                                            (nunitChecker.HasTestAttribute(methodSymbol) ||
                                             methodSymbolsToDelete.Contains(methodSymbol))
                        ))
                    {
                        AddMethodToDelete(syncMethod.Symbol, syncMethod.Node, documentId);
                        foreach (var usage in syncUsages)
                        {
                            var usageCallingSymbol = (IMethodSymbol)usage.CallingSymbol;
                            await AddMethodToDeleteAsync(usageCallingSymbol,
                                                         usageCallingSymbol.DeclaringSyntaxReferences.First());
                        }

                        await TryAddMethodImplementationsToDeleteAsync(syncMethod.Symbol);

                        methodSymbolToSyncAsyncPair.Remove(pair.Key);
                    }
                }
            } while (methodsToDeletePrevCount != methodsToDelete.Count);

            await DeleteCollectedMethodsAsync();
        }

        private async Task DeleteCollectedMethodsAsync()
        {
            if (methodsToDelete.Any())
            {
                Log.Information("{MethodCount} methods to remove, deleting...", methodsToDelete.Count);
                var solutionEditor = new SolutionEditor(Solution);
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
                Console.Write(
                    $"Should the tool delete {methodName}? It might help to automatically detect and delete unused methods that {methodName} used, so prefer this to manual deletion (y/n) ");
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

        private void AddMethodToDelete(IMethodSymbol symbol, MethodDeclarationSyntax syntax, DocumentId documentId)
        {
            if (methodSymbolsToDelete.Add(symbol))
            {
                methodsToDelete.Add(syntax, documentId);
            }
        }

        private async Task AddMethodToDeleteAsync(IMethodSymbol symbol, SyntaxReference reference)
        {
            var document = Solution.GetDocument(reference.SyntaxTree);

            if (document == null)
            {
                Log.Error("Failed to find document for syntax reference in {Path} {Span}",
                          reference.SyntaxTree.FilePath,
                          reference.Span.ToString());
                return;
            }

            if (await reference.GetSyntaxAsync() is not MethodDeclarationSyntax syntax)
            {
                Log.Error("Failed to find method declaration syntax for syntax reference in {Path} {Span}",
                          reference.SyntaxTree.FilePath, reference.Span.ToString());
                return;
            }

            AddMethodToDelete(symbol, syntax, document.Id);
        }

        private async Task TryAddMethodImplementationsToDeleteAsync(IMethodSymbol symbol)
        {
            foreach (var method in (await Solution.FindOverridesAndImplementationsAsync(symbol))
                    .OfType<IMethodSymbol>())
            {
                var callers = (await SymbolFinder.FindCallersAsync(method, Solution)).ToList();

                if (!callers.All(
                        c =>
                        {
                            if (c.CallingSymbol is not IMethodSymbol callingMethod)
                            {
                                return false;
                            }

                            if (methodSymbolsToDelete.Contains(callingMethod))
                            {
                                return true;
                            }

                            var sourceLocation = c.Locations.FirstOrDefault(l => l.IsInSource);
                            var document = Solution.GetDocument(sourceLocation?.SourceTree);

                            if (document == null || !document.TryGetSemanticModel(out var model))
                            {
                                return false;
                            }

                            var checker = TestAttributeChecker.GetInstance(model.Compilation);

                            return checker.HasTestAttribute(callingMethod);
                        }))
                    continue;

                var syntaxReference = method.DeclaringSyntaxReferences.FirstOrDefault();

                if (syntaxReference == null)
                    continue;

                await AddMethodToDeleteAsync(method, syntaxReference);

                await TryAddMethodImplementationsToDeleteAsync(method);
            }
        }
    }
}
