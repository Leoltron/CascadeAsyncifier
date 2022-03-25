using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly HashSet<MethodDeclarationSyntax> ignoredMethods = new();
        private readonly HashSet<MethodDeclarationSyntax> unreportedInOutRefMethods = new();

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
            if (IsAsyncificationCandidate(node))
            {
                base.VisitMethodDeclaration(node);
            }
            else
            {
                CandidateBlacklisted?.Invoke(node);
                ignoredMethods.Add(node);
            }
        }

        private bool IsAsyncificationCandidate(MethodDeclarationSyntax node)
        {
            var methodSymbol = model.GetDeclaredSymbol(node);

            if (matcher.CanBeAsyncified(methodSymbol)/* || testAttributeChecker.HasTestAttribute(methodSymbol)*/)
                return false;

            if (node.ParameterList.Parameters.Any(
                    p =>
                        p.Modifiers.Any(SyntaxKind.OutKeyword) ||
                        p.Modifiers.Any(SyntaxKind.InKeyword) ||
                        p.Modifiers.Any(SyntaxKind.RefKeyword)))
            {
                unreportedInOutRefMethods.Add(node);
                
                return false;
            }

            return true;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);

            if (InAsyncMethod || CurrentMethod == null)
            {
                return;
            }

            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol methodSymbol)
            {
                return;
            }            
            
            var canBeAsyncified = new Lazy<bool>(() => matcher.CanBeAsyncified(methodSymbol));

            if (unreportedInOutRefMethods.Contains(CurrentMethod) && canBeAsyncified.Value)
            {
                LogHelper.CantAsyncifyInOutRefMethod(CurrentMethod.Identifier.Text, node.GetLocation().GetLineSpan());
                unreportedInOutRefMethods.Remove(CurrentMethod);
                return;
            }

            if (ignoredMethods.Contains(CurrentMethod) || node.IsInNoAwaitBlock())
            {
                return;
            }

            if (!canBeAsyncified.Value)
            {
                return;
            }

            if (!model.GetDeclaredSymbol(CurrentMethod).WholeHierarchyChainIsInSourceCode())
            {
                return;
            }

            CandidateFound?.Invoke(CurrentMethod);
            ignoredMethods.Add(CurrentMethod);
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
