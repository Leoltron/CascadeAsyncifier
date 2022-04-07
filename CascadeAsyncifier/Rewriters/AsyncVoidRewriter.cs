using System.Threading.Tasks;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CascadeAsyncifier.Rewriters
{
    public class AsyncVoidRewriter : CSharpSyntaxRewriter
    {
        private bool addTaskUsingIfNeeded;

        public override SyntaxNode
            VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var visitedNode = (CompilationUnitSyntax)base.VisitCompilationUnit(node);

            return addTaskUsingIfNeeded
                ? visitedNode.WithTasksUsingDirective()
                : visitedNode;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var visitedNode = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);

            if (visitedNode == null || !ContainsAsyncVoid(visitedNode))
                return visitedNode;

            addTaskUsingIfNeeded = true;

            return visitedNode
                .WithReturnType(
                    IdentifierName(nameof(Task))
                        .WithTriviaFrom(visitedNode.ReturnType));
        }

        private static bool ContainsAsyncVoid(MethodDeclarationSyntax node) =>
            node.IsAsync() &&
            node.ReturnType is PredefinedTypeSyntax predefinedTypeSyntax &&
            predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.VoidKeyword);
    }
}