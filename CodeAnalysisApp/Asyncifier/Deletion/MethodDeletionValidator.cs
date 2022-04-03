using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Serilog;

namespace CodeAnalysisApp.Asyncifier.Deletion
{
    public class MethodDeletionValidator
    {
        private readonly Solution solution;
        private readonly Document document;
        private readonly SyntaxNode root;
        private readonly SemanticModel model;
        private readonly UndeletableSyncMethodChecker undeletableMethodChecker;
        private readonly DeletableSyncMethodsChecker deletableMethodChecker;

        private MethodDeletionValidator(Solution solution, Document document, SyntaxNode root, SemanticModel model)
        {
            this.solution = solution;
            this.document = document;
            this.root = root;
            this.model = model;
            undeletableMethodChecker = new UndeletableSyncMethodChecker(model.Compilation);
            deletableMethodChecker = DeletableSyncMethodsChecker.GetInstance(model.Compilation);
        }

        public static async Task<MethodDeletionValidator> BuildAsync(Solution solution, Document oldDocument)
        {
            var document = solution.GetDocument(oldDocument.Id);
            if (document == null)
            {
                Log.Error("Failed to find document {DocPath} by id in solution {Solution}", oldDocument.FilePath,
                          solution.FilePath);
                return null;
            }

            var root = await document!.GetSyntaxRootAsync();
            if (root == null)
            {
                Log.Error("Failed to retrieve syntax root for document {DocPath}", document.FilePath);
                return null;
            }

            var model = await document.GetSemanticModelAsync();
            if (model == null)
            {
                Log.Error("Failed to retrieve semantic model for document {DocPath}", document.FilePath);
                return null;
            }

            return new MethodDeletionValidator(solution, document, root, model);
        }


        public async Task<MethodDeletionValidationResult> ValidateAsync(MethodSyntaxSemanticPair method)
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
                return MethodDeletionValidationResult.Ignore;
            }

            var methodSymbol = model.GetDeclaredSymbol(methodSyntax);
            if (methodSymbol == null)
            {
                Log.Error("Failed to find a symbol for previously found method {Method} in document {DocPath}",
                          method.Node.Identifier.Text, document.FilePath);
                return MethodDeletionValidationResult.Ignore;
            }

            if (undeletableMethodChecker.ShouldKeepMethod(methodSymbol))
            {
                return MethodDeletionValidationResult.Ignore;
            }

            if (deletableMethodChecker.CanDeleteSyncMethodWithAsyncOverload(methodSymbol))
            {
                return new MethodDeletionValidationResult(MethodDeletionAction.DeleteAlways, document, methodSyntax,
                                                          methodSymbol);
            }

            if (methodSymbol.FindOverridenOrImplementedSymbol() != null)
            {
                return MethodDeletionValidationResult.Ignore;
            }

            var usagesCount =
                (await SymbolFinder.FindCallersAsync(methodSymbol, solution)).Sum(
                    c => c.Locations.Count());

            var nextSibling = methodSyntax.GetNextSibling();
            if (nextSibling is not MethodDeclarationSyntax asyncMethodSyntax)
            {
                Log.Error(
                    "Expected next sibling of {Method} to be its async version " +
                    "(since its where tool inserted it) in document {DocPath}, found {ActualSibling}",
                    method.Node.Identifier.Text,
                    document.FilePath,
                    nextSibling.Kind().ToString());
                return MethodDeletionValidationResult.Ignore;
            }

            var asyncMethodSymbol = model.GetDeclaredSymbol(asyncMethodSyntax);
            if (asyncMethodSymbol == null)
            {
                Log.Error(
                    "Failed to find a symbol for {Method} (async overload of {SyncMethod}) in document {DocPath}",
                    asyncMethodSyntax.Identifier.Text, methodSyntax.Identifier.Text, document.FilePath);
                return MethodDeletionValidationResult.Ignore;
            }

            var asyncUsagesCount = (await SymbolFinder.FindCallersAsync(asyncMethodSymbol, solution))
               .Sum(c => c.Locations.Count());

            Log.Verbose(
                "Usage stats for {Sync} - {Usages} usages, for {Async} - {AsyncUsages} usages",
                methodSymbol.Name,
                usagesCount,
                asyncMethodSymbol.Name,
                asyncUsagesCount);

            if (asyncUsagesCount == 0)
            {
                if (usagesCount == 0)
                {
                    return new MethodDeletionValidationResult(MethodDeletionAction.AskUser, document, methodSyntax,
                                                              methodSymbol);
                }
                else
                {
                    Log.Warning(
                        "Async overload of method {2}.{0} ({1}) is not used. " +
                        "This means the tool failed to automatically use {1} instead of {0} and user have to" +
                        " manually check {0}'s usages and, if possible, use {1}",
                        methodSymbol.Name,
                        asyncMethodSymbol.Name,
                        methodSymbol.ContainingType.Name);
                    return MethodDeletionValidationResult.Ignore;
                }
            }

            return new MethodDeletionValidationResult(MethodDeletionAction.DeleteCandidate, document, methodSyntax,
                                                      methodSymbol);
        }
    }
}
