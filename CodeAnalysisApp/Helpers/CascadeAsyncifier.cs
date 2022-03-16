using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
using CodeAnalysisApp.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharp.CSharpExtensions;

namespace CodeAnalysisApp.Helpers
{
    public class CascadeAsyncifier
    {
        private readonly DocumentFilter documentFilter = new DocumentFilter();
        private readonly ConcurrentDictionary<IMethodSymbol, bool> blacklistedMethods = new(SymbolEqualityComparer.Default);
        
        public async Task Start(Workspace workspace)
        {
            Console.WriteLine("Asyncifiaction started");
            Console.Write("Collecting initial methods with async overload... ");
            
            var solution = workspace.CurrentSolution;
            var matchers = new Dictionary<ProjectId, AsyncifiableMethodsMatcher>();
            foreach (var project in solution.Projects)
            {
                var matcher = new AsyncifiableMethodsMatcher(await project.GetCompilationAsync());
                matcher.FillAsyncifiableMethodsFromCompilation();
                matchers[project.Id] = matcher;
            }
            
            Console.WriteLine("Done.");
            
            var docTasks = solution.Projects
                .SelectMany(p =>
                                    {
                                        var matcher = matchers[p.Id];
                                        return p.Documents.Where(d => !documentFilter.IgnoreDocument(d)).Select(document => (document, task: TraverseDocument(document, matcher)));
                                    })
                .ToList();
            
            
            Console.WriteLine("Total documents: "+docTasks.Count);
            Console.Write("Collecting initial asyncifiable methods... ");

            await Task.WhenAll(docTasks.Select(p => p.task));
            Console.WriteLine("Done.");
            Console.Write("Looking for asyncifiable methods through initial asyncifiable methods usage and hierarchy... ");

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
                
                if (methodSymbol.IsAbstract)
                {
                    foreach (var method in (await SymbolFinder.FindOverridesAsync(methodSymbol, solution)).Concat(await SymbolFinder.FindImplementationsAsync(methodSymbol, solution)).OfType<IMethodSymbol>())
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
                if(document == null)
                    return;

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
            Console.WriteLine("Done.");

            
            var docClassPairs = documentToClasses.Where(v => v.Value.Any()).ToList();
            var totalClasses = classToMethods.Count(c => c.Value.Any());
            
            var totalMethods = classToMethods.Sum(d => d.Value.Count);
            var methodsIndex = 0;
            
            Console.WriteLine($"Total of {docClassPairs.Count} docs, {totalClasses} types and {totalMethods} methods");
            
            if(totalMethods == 0)
                return;
            
            Console.WriteLine("Duplicating and asyncifing signature of methods... ");

            var slnEditor = new SolutionEditor(workspace.CurrentSolution);
            foreach (var docClassPair in docClassPairs)
            {
                if(documentFilter.IgnoreDocument(docClassPair.Key))
                    continue;
                
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
            Console.WriteLine($"\r{(1):P} Done.");

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
