using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Asyncifier.Deletion
{
    public class MethodDeletionValidationResult
    {
        public MethodDeletionAction Action { get; }
        public Document Document { get; }
        public MethodDeclarationSyntax Syntax { get; }
        public IMethodSymbol Symbol { get; }

        public MethodDeletionValidationResult(MethodDeletionAction action, Document document=null, MethodDeclarationSyntax syntax=null, IMethodSymbol symbol=null)
        {
            Action = action;
            Document = document;
            Syntax = syntax;
            Symbol = symbol;
        }

        public static readonly MethodDeletionValidationResult Ignore = new(MethodDeletionAction.Ignore);
    }
}
