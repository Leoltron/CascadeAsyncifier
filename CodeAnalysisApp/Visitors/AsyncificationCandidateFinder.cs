using System;
using System.Collections.Generic;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Helpers;
using CodeAnalysisApp.Rewriters;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Visitors
{
    public class AsyncificationCandidateFinder : InAsyncMethodContextWalker
    {
        public event Action<MethodDeclarationSyntax> CandidateFound;
        public event Action<MethodDeclarationSyntax> CandidateBlacklisted;

        private readonly SemanticModel model;
        private readonly AsyncifiableMethodsMatcher matcher;
        private readonly AwaitableChecker awaitableChecker;
        private readonly NUnitTestAttributeChecker testAttributeChecker;
        private readonly HashSet<MethodDeclarationSyntax> ignoredCandidates = new();

        public AsyncificationCandidateFinder(
            SemanticModel model,
            AsyncifiableMethodsMatcher matcher)
        {
            this.model = model;
            this.matcher = matcher;
            this.awaitableChecker = new AwaitableChecker(model.Compilation);
            testAttributeChecker = new NUnitTestAttributeChecker(model.Compilation);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var methodSymbol = model.GetDeclaredSymbol(node);
            if (matcher.CanBeAsyncified(methodSymbol)/* || testAttributeChecker.HasTestAttribute(methodSymbol)*/)
            {
                CandidateBlacklisted?.Invoke(node);
                ignoredCandidates.Add(node);
            }
            else
            {
                base.VisitMethodDeclaration(node);
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);

            if (InAsyncMethod)
            {
                return;
            }

            if (CurrentMethod == null || ignoredCandidates.Contains(CurrentMethod) || node.IsInNoAwaitBlock())
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

            if (!model.GetDeclaredSymbol(CurrentMethod).WholeHierarchyChainIsInSourceCode())
            {
                return;
            }

            CandidateFound?.Invoke(CurrentMethod);
            ignoredCandidates.Add(CurrentMethod);
        }

        public override void VisitYieldStatement(YieldStatementSyntax node)
        {
            if (CurrentMethod == null)
            {
                return;
            }

            CandidateBlacklisted?.Invoke(CurrentMethod);
            base.VisitYieldStatement(node);
        }
    }
}
