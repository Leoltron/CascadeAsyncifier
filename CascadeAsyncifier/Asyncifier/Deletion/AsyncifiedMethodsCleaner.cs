using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CascadeAsyncifier.Utils;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Serilog;

namespace CascadeAsyncifier.Asyncifier.Deletion
{
    [SuppressMessage("ReSharper", "PositionalPropertyUsedProblem")]
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

            methodsToDelete = new Dictionary<MethodDeclarationSyntax, DocumentId>();
            methodSymbolsToDelete = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            var unusedMethods = await FilterUnusedMethods(asyncifiedMethodsHierarchy);

            int methodsToDeletePrevCount;
            do
            {
                methodsToDeletePrevCount = methodsToDelete.Count;

                foreach (var candidate in unusedMethods.ToList())
                {
                    var (documentId, method) = candidate.Value;
                    var document = Solution.GetDocument(documentId);
                    var semanticModel = await document.GetSemanticModelAsync();
                    var testAttrChecker = TestAttributeChecker.GetInstance(semanticModel.Compilation);

                    var usages =
                        (await SymbolFinder.FindCallersAsync(method.Symbol, Solution)).ToList();

                    if (usages.All(u => u.CallingSymbol is IMethodSymbol methodSymbol &&
                                        (testAttrChecker.HasTestAttribute(methodSymbol) ||
                                         methodSymbolsToDelete.Contains(methodSymbol))
                        ))
                    {
                        AddMethodToDelete(method.Symbol, method.Node, documentId);
                        foreach (var caller in usages.Select(u => (IMethodSymbol) u.CallingSymbol))
                        {
                            await AddMethodToDeleteAsync(caller, caller.DeclaringSyntaxReferences.First());
                        }

                        await TryAddMethodImplementationsToDeleteAsync(method.Symbol);

                        unusedMethods.Remove(candidate.Key);
                    }
                }
            } while (methodsToDeletePrevCount != methodsToDelete.Count);

            await DeleteCollectedMethodsAsync();
        }

        private async Task<Dictionary<IMethodSymbol, (DocumentId documentId, MethodSyntaxSemanticPair method)>>
            FilterUnusedMethods(DocTypeMethodHierarchy asyncifiedMethodsHierarchy)
        {
            Log.Information("Scanning usages to determine which methods became obsolete and can be deleted");

            var deletionCandidates =
                new Dictionary<IMethodSymbol, (DocumentId documentId, MethodSyntaxSemanticPair method)>(
                    SymbolEqualityComparer.Default);
            var candidatesToPromptDeletion = new List<(Document document, MethodSyntaxSemanticPair method)>();

            Log.Information("Collecting generated methods syntax references");
            foreach (var (oldDocument, types) in asyncifiedMethodsHierarchy.DocsToTypes)
            {
                var deletionValidator = await MethodDeletionValidator.BuildAsync(Solution, oldDocument);
                if (deletionValidator == null)
                    continue;

                foreach (var method in types.SelectMany(pair => asyncifiedMethodsHierarchy.TypeToMethods[pair]))
                {
                    var result = await deletionValidator.ValidateAsync(method);
                    switch (result.Action)
                    {
                        case MethodDeletionAction.DeleteCandidate:
                            deletionCandidates.Add(result.Symbol, (result.Document.Id, (result.Syntax, result.Symbol)));
                            break;
                        case MethodDeletionAction.DeleteAlways:
                            await AddMethodToDeleteAsync(result.Symbol,
                                                         result.Symbol.DeclaringSyntaxReferences.First());
                            break;
                        case MethodDeletionAction.AskUser:
                            candidatesToPromptDeletion.Add((result.Document, (result.Syntax, result.Symbol)));
                            break;
                        case MethodDeletionAction.Ignore:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                $"Unknown {nameof(MethodDeletionAction)} value: {result.Action}");
                    }
                }
            }

            foreach (var tuple in GetCandidatesUserAllowedToDelete(candidatesToPromptDeletion))
            {
                deletionCandidates.Add(tuple.method.Symbol, tuple);
            }

            return deletionCandidates;
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

                if (!workspace.TryApplyChanges(solutionEditor.GetChangedSolution()))
                {
                    Log.Error("Could not apply changes to the solution, method deletion aborted");
                }
                else
                {
                    Log.Information("Done");
                }
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

                if (!callers.All(CallingMethodCanBeDeletedWithCalledMethod))
                    continue;

                var syntaxReference = method.DeclaringSyntaxReferences.FirstOrDefault();

                if (syntaxReference == null)
                    continue;

                await AddMethodToDeleteAsync(method, syntaxReference);
                await TryAddMethodImplementationsToDeleteAsync(method);
            }
        }

        private bool CallingMethodCanBeDeletedWithCalledMethod(SymbolCallerInfo callerInfo)
        {
            if (callerInfo.CallingSymbol is not IMethodSymbol callingMethod)
            {
                return false;
            }

            if (methodSymbolsToDelete.Contains(callingMethod))
            {
                return true;
            }

            var sourceLocation = callerInfo.Locations.FirstOrDefault(l => l.IsInSource);
            var document = Solution.GetDocument(sourceLocation?.SourceTree);

            if (document == null || !document.TryGetSemanticModel(out var model))
            {
                return false;
            }

            return TestAttributeChecker.GetInstance(model.Compilation).HasTestAttribute(callingMethod);
        }

        private static IEnumerable<(DocumentId documentId, MethodSyntaxSemanticPair method)>
            GetCandidatesUserAllowedToDelete(
                IReadOnlyList<(Document document, MethodSyntaxSemanticPair method)> candidatesToPromptDeletion)
        {
            if (!candidatesToPromptDeletion.Any())
                yield break;

            Log.Warning(
                "Some of methods are not used along with their new async overloads. Prompting user to decide their fate");
            foreach (var (document, syncPair) in candidatesToPromptDeletion)
            {
                Log.Warning("Prompting about {Method} in {Path}", syncPair.Symbol.Name, document.FilePath);

                var shouldDelete = PromptUserToDelete(syncPair.Symbol.Name);
                if (shouldDelete)
                {
                    Log.Warning("{Path} - {Method} will be deleted", document.FilePath, syncPair.Symbol.Name);
                    yield return (document.Id, syncPair);
                }
                else
                {
                    Log.Warning("{Path} - {Method} is ignored", document.FilePath, syncPair.Symbol.Name);
                }
            }
        }
    }
}
