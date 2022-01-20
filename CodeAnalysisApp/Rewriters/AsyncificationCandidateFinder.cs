using System;
using System.Collections.Generic;
using System.Linq;
using CodeAnalysisApp.Helpers.SyncAsyncMethodPairProviders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class AsyncificationCandidateFinder : InAsyncMethodContextWalker
    {
        public event Action<MethodDeclarationSyntax> CandidateFound;
        
        private readonly SemanticModel model;
        private readonly ISyncAsyncMethodPairProvider syncAsyncMethodPairProvider;
        private readonly HashSet<MethodDeclarationSyntax> foundCandidates = new();

        public AsyncificationCandidateFinder(SemanticModel model) :
            this(model, new HardcodeSyncAsyncMethodPairProvider())
        {
        }

        public AsyncificationCandidateFinder(
            SemanticModel model,
            ISyncAsyncMethodPairProvider syncAsyncMethodPairProvider)
        {
            this.model = model;
            this.syncAsyncMethodPairProvider = syncAsyncMethodPairProvider;
        }

        public void ResetCandidatesCache()
        {
            foundCandidates.Clear();
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);
            
            if (InAsyncMethod)
            {
                return;
            }

            if (CurrentMethod == null || foundCandidates.Contains(CurrentMethod))
            {
                return;
            }

            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
            {
                return;
            }

            var matchingMethod = syncAsyncMethodPairProvider.Provide().FirstOrDefault(m => m.MatchSyncMethod(symbol));

            if (matchingMethod == null)
            {
                return;
            }

            CandidateFound?.Invoke(CurrentMethod);
            foundCandidates.Add(CurrentMethod);
        }
    }
}
