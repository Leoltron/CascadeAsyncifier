using System.Collections.Generic;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Rewriters.Base
{
    public abstract class InAsyncMethodContextRewriter : FunctionContextRewriter
    {
        protected bool InAsyncMethod
        {
            get => CurrentContext.GetOrDefault("InAsyncMethod", false);
            private set => CurrentContext["InAsyncMethod"] = value;
        }

        protected CSharpSyntaxNode CurrentMethod
        {
            get => CurrentContext.GetOrDefault("CurrentMethod", (CSharpSyntaxNode) null);
            private set => CurrentContext["CurrentMethod"] = value;
        }

        protected ITypeSymbol GetCurrentMethodReturnType(SemanticModel model)
        {
            var currentMethod = CurrentMethod;

            if (currentMethod == null)
                return null;

            var methodSymbol = model.GetDeclaredSymbol(currentMethod) as IMethodSymbol ?? (IMethodSymbol) model.GetSymbolInfo(currentMethod).Symbol;

            return methodSymbol!.ReturnType;
        }

        protected override void BeforeSimpleLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            SimpleLambdaExpressionSyntax node)
        {
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = node;
        }

        protected override void BeforeParenthesizedLambdaExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            ParenthesizedLambdaExpressionSyntax node)
        {
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = node;
        }

        protected override void BeforeAnonymousMethodExpressionVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            AnonymousMethodExpressionSyntax node)
        {
            InAsyncMethod = !node.AsyncKeyword.IsEmpty();
            CurrentMethod = node;
        }

        protected override void BeforeMethodDeclarationVisit(IDictionary<string, object> parentContext, IDictionary<string, object> nodeContext, MethodDeclarationSyntax node)
        {
            InAsyncMethod = node.IsAsync();
            CurrentMethod = node;
        }

        protected override void BeforeLocalFunctionStatementVisit(
            IDictionary<string, object> parentContext,
            IDictionary<string, object> nodeContext,
            LocalFunctionStatementSyntax node)
        {
            InAsyncMethod = node.IsAsync();
            CurrentMethod = node;
        }
    }
}
