using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp
{
    public class TraverseResult
    {
        public Document NewDocument { get; }
        public bool ReloadSolution { get; }
        public bool RestartTraverse { get; }

        public TraverseResult(Document newDocument, bool reloadSolution, bool restartTraverse)
        {
            NewDocument = newDocument;
            ReloadSolution = reloadSolution;
            RestartTraverse = restartTraverse;
        }

        public static readonly TraverseResult Continue = new(null, false, false);
        public static TraverseResult Reload(Document newDoc) => new(newDoc, true, false);
        public static TraverseResult ReloadAndRestartTraverse(Document newDoc) => new(newDoc, true, true);
    }
}
