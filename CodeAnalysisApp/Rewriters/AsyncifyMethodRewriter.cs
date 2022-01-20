using System;
using System.Collections.Generic;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeAnalysisApp.Rewriters
{
    public class AsyncifyMethodRewriter : InAsyncMethodContextRewriter
    {
        private readonly SemanticModel model;

        public AsyncifyMethodRewriter(SemanticModel model)
        {
            this.model = model;
        }

        protected override SyntaxNode VisitMethodDeclarationWithContext(MethodDeclarationSyntax node)
        {
            if (FunctionDepth != 1)
                return node;
            
            if (InAsyncMethod)
                throw new Exception("Target method is asynchronous");

            return node
                .WithIdentifier(Identifier(node.Identifier.Text + "Async"))
                .AddAsyncModifier()
                .WithReturnType(AsyncifyReturnType(node));

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
    }
}
