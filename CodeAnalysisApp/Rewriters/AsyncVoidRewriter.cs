using System.Threading.Tasks;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp.Rewriters
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

            if (!ContainsAsyncVoid(visitedNode))
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
