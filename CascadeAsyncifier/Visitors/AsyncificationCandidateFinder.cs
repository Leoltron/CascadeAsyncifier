using System;
using System.Collections.Generic;
using System.Linq;
using CascadeAsyncifier.Asyncifier.Matchers;
using CascadeAsyncifier.Helpers;
using CascadeAsyncifier.Extensions;
using CascadeAsyncifier.Rewriters.Base;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Visitors
{
    public class AsyncificationCandidateFinder : InAsyncMethodContextWalker
    {
        public event Action<MethodDeclarationSyntax> CandidateFound;
        public event Action<MethodDeclarationSyntax> CandidateBlacklisted;

        private readonly SemanticModel model;
        private readonly AsyncOverloadMatcher matcher;
        private readonly ISpecialAsyncifiableMethodMatcher[] specialMatchers;
        private readonly HashSet<MethodDeclarationSyntax> ignoredMethods = new();
        private readonly HashSet<MethodDeclarationSyntax> unreportedInOutRefMethods = new();

        public AsyncificationCandidateFinder(
            SemanticModel model,
            AsyncOverloadMatcher matcher)
        {
            this.model = model;
            this.matcher = matcher;
            specialMatchers = new ISpecialAsyncifiableMethodMatcher[] { new EntityFrameworkQueryableMethodMatcher(model) }
                .Where(m => m.CanBeUsed)
                .ToArray();
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

            if (matcher.HasAsyncOverload(methodSymbol))
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
            
            if (!methodSymbol.WholeHierarchyChainIsInSourceCode())
            {
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

            if (ignoredMethods.Contains(CurrentMethod) || node.IsInNoAwaitBlock())
            {
                return;
            }

            var canBeAsyncified = new Lazy<bool>(
                () => matcher.HasAsyncOverload(methodSymbol) ||
                      specialMatchers.Any(m => m.TryGetAsyncMethod(node, out _)));

            if (unreportedInOutRefMethods.Contains(CurrentMethod) && canBeAsyncified.Value)
            {
                LogHelper.CantAsyncifyInOutRefMethod(CurrentMethod.Identifier.Text, node.GetLocation().GetLineSpan());
                unreportedInOutRefMethods.Remove(CurrentMethod);
                return;
            }

            if (!canBeAsyncified.Value)
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
