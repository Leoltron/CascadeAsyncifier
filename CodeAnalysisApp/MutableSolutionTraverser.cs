using System;
using System.Collections.Generic;
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


        public async Task TraverseAsync(Func<Document, Task<TraverseResult>> action)
        {
            while (true)
            {
                var traverserAction = await TraverseInternalAsync(action);

                if (!traverserAction.ReloadSolution && !traverserAction.RestartTraverse)
                    return;

                if (fullReloads > maxFullReloads)
                    throw new Exception($"Exceeded max full reloads ({maxFullReloads})");

                if (oneDocReloads > maxOneDocReloads)
                    throw new Exception(
                        $"Exceeded max reloads ({maxFullReloads}) for one document ({workspace.CurrentSolution.GetDocument(currentDocumentId)?.FilePath})");
            }
        }

        private async Task<TraverseResult> TraverseInternalAsync(Func<Document, Task<TraverseResult>> action)
        {
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    var traverseResult = await action(document);

                    if (traverseResult.RestartTraverse)
                    {
                        traversedDocs.Clear();
                    }
                    else
                    {
                        traversedDocs.Add(document.Id);
                    }

                    if (!traverseResult.ReloadSolution && !traverseResult.RestartTraverse)
                        continue;

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
                    else if (currentDocumentId !=null && currentDocumentId.Equals(document.Id))
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
