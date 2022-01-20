using System.Linq;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp
{
    public static class MethodSignatureExtensions
    {
        public static MethodDeclarationSyntax WithAsyncSignatureAndName(this MethodDeclarationSyntax methodDeclaration)
        {
            return methodDeclaration
                .WithIdentifier(Identifier(methodDeclaration.Identifier.Text + "Async"))
                .AddAsyncModifier()
                .WithReturnType(AsyncifyReturnType(methodDeclaration));
        }

        private static TypeSyntax AsyncifyReturnType(MethodDeclarationSyntax node)
        {
            var returnType = node.ReturnType;

            if (node.ReturnsVoid())
                return IdentifierName("Task").WithTriviaFrom(returnType);
            
            return GenericName(Identifier("Task"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(returnType.WithoutTrailingTrivia())))
                .WithTriviaFrom(returnType);
        }
        
        public static MethodDeclarationSyntax WithoutAsyncModifier(this MethodDeclarationSyntax methodDeclaration)
        {
            var asyncModifier =
                methodDeclaration.Modifiers.FirstOrDefault(m => m.Kind() == SyntaxKind.AsyncKeyword);

            return asyncModifier == default
                ? methodDeclaration
                : methodDeclaration.WithModifiers(methodDeclaration.Modifiers.Remove(asyncModifier));
        }

        public static MethodDeclarationSyntax AddAsyncModifier(this MethodDeclarationSyntax methodDeclaration)
        {
            return methodDeclaration.AddModifiers(Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(Space));
        }

        public static bool ReturnsVoid(this MethodDeclarationSyntax methodDeclaration) =>
            methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedReturnType &&
            predefinedReturnType.Keyword.IsKind(SyntaxKind.VoidKeyword);
        
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
                : lambdaExpression.WithAsyncKeyword(Token(SyntaxKind.None));
        }

        public static SimpleLambdaExpressionSyntax WithoutAsyncModifier(this SimpleLambdaExpressionSyntax simpleLambdaExpression)
        {
            return simpleLambdaExpression.AsyncKeyword.IsEmpty()
                ? simpleLambdaExpression
                : simpleLambdaExpression.WithAsyncKeyword(Token(SyntaxKind.None));
        }

        public static AnonymousMethodExpressionSyntax WithoutAsyncModifier(this AnonymousMethodExpressionSyntax simpleLambdaExpression)
        {
            return simpleLambdaExpression.AsyncKeyword.IsEmpty()
                ? simpleLambdaExpression
                : simpleLambdaExpression.WithAsyncKeyword(Token(SyntaxKind.None));
        }
    }
}
