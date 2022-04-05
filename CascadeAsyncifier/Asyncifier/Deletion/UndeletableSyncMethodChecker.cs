using CascadeAsyncifier.Utils;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Asyncifier.Deletion
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
