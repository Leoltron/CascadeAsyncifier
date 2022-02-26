using System;
using System.Collections.Generic;
using CodeAnalysisApp.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public class AsyncificationCandidateFinder : InAsyncMethodContextWalker
    {
        public event Action<MethodDeclarationSyntax> CandidateFound;
        
        private readonly SemanticModel model;
        private readonly AsyncifiableMethodsMatcher matcher;
        private readonly HashSet<MethodDeclarationSyntax> foundCandidates = new();

        public AsyncificationCandidateFinder(
            SemanticModel model,
            AsyncifiableMethodsMatcher matcher)
        {
            this.model = model;
            this.matcher = matcher;
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

            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol methodSymbol)
            {
                return;
            }

            if (!matcher.CanBeAsyncified(methodSymbol))
            {
                return;
            }

            CandidateFound?.Invoke(CurrentMethod);
            foundCandidates.Add(CurrentMethod);
        }
    }
}
