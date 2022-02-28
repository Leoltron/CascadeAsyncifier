using System.Collections.Generic;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Rewriters
{
    public abstract class InAsyncMethodContextRewriter : FunctionContextRewriter
    {
        protected bool InAsyncMethod
        {
            get => CurrentContext.GetOrDefault("InAsyncMethod", false);
            private set => CurrentContext["InAsyncMethod"] = value;
        }

        protected override void BeforeSimpleLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SimpleLambdaExpressionSyntax node)
        {
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
        }

        protected override void BeforeParenthesizedLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            ParenthesizedLambdaExpressionSyntax node)
        {
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
        }

        protected override void BeforeAnonymousMethodExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            AnonymousMethodExpressionSyntax node)
        {
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
        }

        protected override void BeforeMethodDeclarationVisit(IDictionary<string, object> parentContext, IDictionary<string, object> nodeContext, MethodDeclarationSyntax node)
        {
            InAsyncMethod = node.IsAsync();
        }

        protected override void BeforeLocalFunctionStatementVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            LocalFunctionStatementSyntax node)
        {
            InAsyncMethod = node.IsAsync();
        }
    }
}
