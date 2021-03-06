using System.Linq;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CascadeAsyncifier
{
    public static class MethodSignatureExtensions
    {
        public static MethodDeclarationSyntax WithAsyncSignatureAndName(this MethodDeclarationSyntax methodDeclaration,
                                                                        bool addAsyncKeyword = true,
                                                                        bool useTaskNamespace = false)
        {
            var asyncMethod = methodDeclaration
               .WithIdentifier(Identifier(methodDeclaration.Identifier.Text + "Async"));

            if (addAsyncKeyword)
                asyncMethod = asyncMethod.AddAsyncModifier();

            return asyncMethod
               .WithReturnType(AsyncifyReturnType(methodDeclaration, useTaskNamespace));
        }

        private static TypeSyntax AsyncifyReturnType(MethodDeclarationSyntax node, bool useTaskNamespace)
        {
            var returnType = node.ReturnType;

            if (node.ReturnsVoid())
                return IdentifierName("Task").WithTriviaFrom(returnType);
            
            var identifierText = useTaskNamespace ? "System.Threading.Tasks.Task" : "Task";
            
            return GenericName(Identifier(identifierText))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(returnType.WithoutTrivia())))
                .WithTriviaFrom(returnType);
        }
        
        public static MethodDeclarationSyntax WithoutAsyncModifier(this MethodDeclarationSyntax methodDeclaration)
        {
            var asyncModifier =
                methodDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.AsyncKeyword));

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
                methodDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.AsyncKeyword));

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

        public static T WithoutRegionTrivia<T>(this T node) where T : SyntaxNode
        {
            return node.ReplaceTrivia(
                node.GetLeadingTrivia()
                    .Concat(node.GetTrailingTrivia())
                    .Where(
                        t => t.IsKind(SyntaxKind.EndRegionDirectiveTrivia) ||
                             t.IsKind(SyntaxKind.RegionDirectiveTrivia)),
                (_, _) => LineFeed);
        }
    }
}
