using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp
{
    public class MutableSolutionTraverser
    {
        private readonly Workspace workspace;
        private readonly HashSet<DocumentId> traversedDocs = new();
        private readonly int maxOneDocReloads;
        private readonly int maxFullReloads;

        private int fullReloads;
        private int oneDocReloads;
        private DocumentId currentDocumentId;

        public MutableSolutionTraverser(
            Workspace workspace,
            int maxOneDocReloads = 20,
            int maxFullReloads = int.MaxValue)
        {
            this.workspace = workspace;
            this.maxOneDocReloads = maxOneDocReloads;
            this.maxFullReloads = maxFullReloads;
        }

        public void Reset()
        {
            traversedDocs.Clear();
        }


        public async Task TraverseAsync(Func<Document, Task<TraverseResult>> action)
        {
            Reset();
            while (true)
            {
                var traverserAction = await TraverseInternalAsync(action);

                if (!traverserAction.ReloadSolution && !traverserAction.RestartTraverse)
                    break;

                if (fullReloads > maxFullReloads)
                    throw new Exception($"Exceeded max full reloads ({maxFullReloads})");

                if (oneDocReloads > maxOneDocReloads)
                    throw new Exception(
                        $"Exceeded max reloads ({maxFullReloads}) for one document ({workspace.CurrentSolution.GetDocument(currentDocumentId)?.FilePath})");
            }

            ReportProgress?.Invoke(1, 1);
        }

        public event Action<int, int> ReportProgress;

        private async Task<TraverseResult> TraverseInternalAsync(Func<Document, Task<TraverseResult>> action)
        {
            var docsInSolution = workspace.CurrentSolution.Projects.Sum(p => p.Documents.Count());
            var docsVisited = 0;
            ReportProgress?.Invoke(0, docsInSolution);
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    if(traversedDocs.Contains(document.Id))
                        continue;
                    
                    var traverseResult = await action(document);

                    if (traverseResult.RestartTraverse)
                    {
                        traversedDocs.Clear();
                    }

                    if (!traverseResult.ReloadSolution && !traverseResult.RestartTraverse){

                        docsVisited++;
                        traversedDocs.Add(document.Id);
                        ReportProgress?.Invoke(docsVisited, docsInSolution);
                        
                        continue;
                    }

                    if (traverseResult.ReloadSolution)
                    {
                        var newSolution = traverseResult.NewDocument.Project.Solution;

                        if (!workspace.TryApplyChanges(newSolution))
                            throw new Exception("Can't apply changes");
                    }

                    if (traverseResult.RestartTraverse)
                    {
                        fullReloads++;
                    }
                    else if (currentDocumentId != null && currentDocumentId.Equals(document.Id))
                    {
                        oneDocReloads++;
                    }
                    else
                    {
                        oneDocReloads = 1;
                        currentDocumentId = document.Id;
                    }

                    return traverseResult;
                }
            }

            return TraverseResult.Continue;
        }
    }
}
