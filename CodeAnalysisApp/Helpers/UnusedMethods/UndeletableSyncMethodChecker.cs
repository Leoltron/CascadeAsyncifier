using System.Runtime.CompilerServices;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Helpers.UnusedMethods
{
    public class UndeletableSyncMethodChecker
    {
        private readonly TestAttributeChecker testAttributeChecker;
        
        public UndeletableSyncMethodChecker(Compilation compilation)
        {
            testAttributeChecker = TestAttributeChecker.GetInstance(compilation);
        }

        public bool ShouldKeepMethod(IMethodSymbol methodSymbol)
        {
            return testAttributeChecker.HasTestAttribute(methodSymbol);
        }
    }
}
