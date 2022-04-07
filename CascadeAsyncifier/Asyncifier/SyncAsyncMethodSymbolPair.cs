using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Asyncifier
{
    public class SyncAsyncMethodSymbolPair
    {
        public IMethodSymbol SyncMethod { get; }
        public IMethodSymbol AsyncMethod { get; }

        public SyncAsyncMethodSymbolPair(IMethodSymbol syncMethod, IMethodSymbol asyncMethod)
        {
            SyncMethod = syncMethod;
            AsyncMethod = asyncMethod;
        }

        public override string ToString() =>
            $"{SyncMethod.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)} -> {AsyncMethod.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}";
    }
}
