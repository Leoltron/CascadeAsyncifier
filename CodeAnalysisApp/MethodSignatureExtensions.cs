using System.Linq;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp
{
    public static class MethodSignatureExtensions
    {
        public static MethodDeclarationSyntax WithoutAsyncModifier(this MethodDeclarationSyntax methodDeclaration)
        {
            var asyncModifier =
                methodDeclaration.Modifiers.FirstOrDefault(m => m.Kind() == SyntaxKind.AsyncKeyword);

            return asyncModifier == default
                ? methodDeclaration
                : methodDeclaration.WithModifiers(methodDeclaration.Modifiers.Remove(asyncModifier));
        }
        public static LocalFunctionStatementSyntax WithoutAsyncModifier(this LocalFunctionStatementSyntax methodDeclaration)
        {
            var asyncModifier =
                methodDeclaration.Modifiers.FirstOrDefault(m => m.Kind() == SyntaxKind.AsyncKeyword);

            return asyncModifier == default
                ? methodDeclaration
                : methodDeclaration.WithModifiers(methodDeclaration.Modifiers.Remove(asyncModifier));
        }

        public static ParenthesizedLambdaExpressionSyntax WithoutAsyncModifier(this ParenthesizedLambdaExpressionSyntax lambdaExpression)
        {
            return lambdaExpression.AsyncKeyword.IsEmpty()
                ? lambdaExpression
                : lambdaExpression.WithAsyncKeyword(SyntaxFactory.Token(SyntaxKind.None));
        }

        public static SimpleLambdaExpressionSyntax WithoutAsyncModifier(this SimpleLambdaExpressionSyntax simpleLambdaExpression)
        {
            return simpleLambdaExpression.AsyncKeyword.IsEmpty()
                ? simpleLambdaExpression
                : simpleLambdaExpression.WithAsyncKeyword(SyntaxFactory.Token(SyntaxKind.None));
        }

        public static AnonymousMethodExpressionSyntax WithoutAsyncModifier(this AnonymousMethodExpressionSyntax simpleLambdaExpression)
        {
            return simpleLambdaExpression.AsyncKeyword.IsEmpty()
                ? simpleLambdaExpression
                : simpleLambdaExpression.WithAsyncKeyword(SyntaxFactory.Token(SyntaxKind.None));
        }
    }
}
