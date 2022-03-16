using System.Collections.Generic;
using CodeAnalysisApp.Extensions;
using CodeAnalysisApp.Rewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Visitors
{
    public class CallingFunctionFinder : InAsyncMethodContextWalker
    {
        private readonly SemanticModel model;
        private IMethodSymbol searchedInvocation;
        private List<IMethodSymbol> callingFunctions;

        private IMethodSymbol CurrentMethodSymbol {
            get
            {
                var currentMethod = CurrentMethod;

                if (currentMethod == null)
                    return null;

                return model.GetDeclaredSymbol(CurrentMethod) as IMethodSymbol ?? (IMethodSymbol) model.GetSymbolInfo(currentMethod).Symbol;
            }
        }

        public CallingFunctionFinder(SemanticModel model)
        {
            this.model = model;
        }


        public IReadOnlyList<IMethodSymbol> GetCallingFunctions(SyntaxNode searchRoot, IMethodSymbol invokedMethod)
        {
            searchedInvocation = invokedMethod;
            callingFunctions = new List<IMethodSymbol>();
            Visit(searchRoot);

            return callingFunctions;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (model.GetSymbolInfo(node).Symbol is IMethodSymbol symbol)
            {
                if (searchedInvocation.SymbolEquals(symbol))
                {
                    callingFunctions.Add(symbol);
                }
            }
            
                
                
            base.VisitInvocationExpression(node);
        }
    }
}
