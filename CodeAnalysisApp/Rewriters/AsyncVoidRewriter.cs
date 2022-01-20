using System.Linq;
using System.Threading.Tasks;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CodeAnalysisApp.ExtendedSyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp.Rewriters
{
    public class AsyncVoidRewriter : CSharpSyntaxRewriter
    {
        private bool addTaskUsingIfNeeded;
        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var baseNode = base.VisitCompilationUnit(node);

            return AddUsingIfNeeded(baseNode);
        }

        private SyntaxNode AddUsingIfNeeded(SyntaxNode baseNode)
        {
            if (baseNode is not CompilationUnitSyntax cu || !addTaskUsingIfNeeded)
                return baseNode;

            if (cu.Usings.Any(u => u.Name.ToString() == "System.Threading.Tasks"))
                return baseNode;

            return cu.AddUsings(UsingDirective("System", "Threading", "Tasks"));
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var visitedNode = base.VisitMethodDeclaration(node);

            if (!ContainsAsyncVoid(node) || visitedNode is not MethodDeclarationSyntax visitedMethodNode)
                return visitedNode;

            addTaskUsingIfNeeded = true;

            return visitedMethodNode.WithReturnType(IdentifierName(nameof(Task)).WithTriviaFrom(visitedMethodNode.ReturnType));
        }

        private static bool ContainsAsyncVoid(MethodDeclarationSyntax node)
        {
            if (!node.IsAsync())
            {
                return false;
            }

            if (node.ReturnType is not PredefinedTypeSyntax predefinedTypeSyntax)
                return false;

            return predefinedTypeSyntax.Keyword.Kind() == SyntaxKind.VoidKeyword;
        }
    }
}
