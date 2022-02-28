using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Helpers.SyncAsyncMethodPairProviders;
using CodeAnalysisApp.Rewriters;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CodeAnalysisApp.Helpers
{
    public class CascadeAsyncifier
    {
        public async Task Start(Workspace workspace)
        {
            var solution = workspace.CurrentSolution;
            var matchers = new Dictionary<ProjectId, AsyncifiableMethodsMatcher>();
            foreach (var project in solution.Projects)
            {
                var matcher = new AsyncifiableMethodsMatcher(await project.GetCompilationAsync());
                matcher.FillAsyncifiableMethodsFromCompilation();
                matchers[project.Id] = matcher;
            }
            
            var docTasks = solution.Projects
                .SelectMany(p =>
                                    {
                                        var matcher = matchers[p.Id];
                                        return p.Documents.Select(document => (document, task: TraverseDocument(document, matcher)));
                                    })
                .ToList();

            await Task.WhenAll(docTasks.Select(p => p.task));

            var documentToClasses = docTasks.ToDictionary(
                docTask => docTask.document,
                d => d.task.Result.Keys.ToList());
            var classToMethods = docTasks.SelectMany(d => d.task.Result).ToDictionary(t => t.Key, t => t.Value);

            var asyncifableMethodsQueue = new Queue<IMethodSymbol>(
                docTasks.SelectMany(d => d.task.Result.Values.SelectMany(l => l.Select(mp => mp.Symbol))));
            var visitedMethods = new HashSet<IMethodSymbol>(asyncifableMethodsQueue, SymbolEqualityComparer.Default);

            while (asyncifableMethodsQueue.Any())
            {
                var methodSymbol = asyncifableMethodsQueue.Dequeue();

                if (methodSymbol.IsAbstract)
                {
                    foreach (var method in (await SymbolFinder.FindOverridesAsync(methodSymbol, solution)).OfType<IMethodSymbol>())
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
                foreach (var callerInfo in callers.Where(f => f.IsDirect))
                {
                    var callingSymbol = (IMethodSymbol)callerInfo.CallingSymbol;
                    if(callingSymbol.WholeHierarchyChainIsInSourceCode())
                        await AddMethod(callingSymbol);
                }
            }


            async Task AddMethod(IMethodSymbol method)
            {
                if (method.MethodKind != MethodKind.Ordinary)
                {
                    return;
                }

                var methodSyntax =
                    (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences.First().GetSyntaxAsync();

                var typeSyntax = methodSyntax.Parent as TypeDeclarationSyntax;;
                var document = solution.GetDocument(methodSyntax.SyntaxTree);
                if(document == null)
                    return;

                var semanticModel = await document.GetSemanticModelAsync();

                var awaitChecker = new AwaitableSyntaxChecker(semanticModel);

                if (methodSyntax.IsAsync() || awaitChecker.IsTypeAwaitable(methodSyntax.ReturnType))
                    return;

                if (!visitedMethods.Add(method) || typeSyntax == null)
                    return;

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
            
            var slnEditor = new SolutionEditor(workspace.CurrentSolution);
            var docClassPairs = documentToClasses.Where(v => v.Value.Any()).ToList();
            foreach (var docClassPair in docClassPairs)
            {
                var document = docClassPair.Key;
                var editor = await slnEditor.GetDocumentEditorAsync(document.Id);
                var root = await document.GetSyntaxRootAsync();

                foreach (var method in docClassPair.Value.SelectMany(pair => classToMethods[pair]))
                {
                    if (matchers[document.Project.Id].CanBeAsyncified(method.Symbol))
                    {
                        continue;
                    }
                    
                    editor.InsertAfter(method.Node, method.Node.WithAsyncSignatureAndName(!method.Symbol.IsAbstract));
                }
                editor.ReplaceNode(root, (n, gen) => n is CompilationUnitSyntax cu ? cu.WithTasksUsingDirective() : n);
            }

            workspace.TryApplyChanges(slnEditor.GetChangedSolution());

        }

        private static async Task<Dictionary<TypeSyntaxSemanticPair, List<MethodSyntaxSemanticPair>>> TraverseDocument(
            Document document, AsyncifiableMethodsMatcher matcher)
        {
            var classToMethods = new Dictionary<ClassDeclarationSyntax, List<MethodDeclarationSyntax>>();

            var visitedMethods = new HashSet<MethodDeclarationSyntax>();

            var root = await document.GetSyntaxRootAsync();
            var model = await document.GetSemanticModelAsync();

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

            finder.Visit(root);

            return classToMethods
                .Select(
                    p => (new TypeSyntaxSemanticPair(p.Key, model.GetDeclaredSymbol(p.Key) as ITypeSymbol),
                        p.Value.Select(
                                m => new MethodSyntaxSemanticPair(m, model.GetDeclaredSymbol(m) as IMethodSymbol))
                            .Where(mp => mp.Symbol != null)
                            .ToList()))
                .Where(p => p.Item1.Symbol != null && p.Item2.Any())
                .ToDictionary(p => p.Item1, p => p.Item2);
        }
    }
}
