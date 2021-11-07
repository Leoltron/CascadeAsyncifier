using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp
{
    public class MyCSharpSyntaxWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel model;
        private readonly INamedTypeSymbol attributeSymbol;
        private const string ATTR_NAME = "TestConsoleApplication.ExpectConfigureAwaitTrueAttribute";

        public MyCSharpSyntaxWalker(SemanticModel model, SyntaxWalkerDepth depth = SyntaxWalkerDepth.Node) : base(depth)
        {
            this.model = model;
            attributeSymbol = model.Compilation.GetTypeByMetadataName(ATTR_NAME);
        }
        
        

        public override void VisitAwaitExpression(AwaitExpressionSyntax node)
        { 
            Console.Write("BOINK " + node);
            var a = model.GetTypeInfo(node.Expression);
            if (a.Type.ToDisplayString().StartsWith("System.Threading.Tasks.Task"))
            {
                Console.Write(" GOTCHA");
            }
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            foreach (var attributeList in node.AttributeLists)
            {
                foreach (var attr in attributeList.Attributes)
                {
                    var a = model.GetTypeInfo(attr);
                }
            }
            base.VisitMethodDeclaration(node);
        }
    }
}
