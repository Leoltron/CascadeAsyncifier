using System;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Helpers.SyncAsyncMethodPairProviders
{
    public class SyncAsyncMethodPair
    {
        public string SyncMethodName { get; }
        public string AsyncMethodName { get; }

        public SyncAsyncMethodPair(string syncMethodName, string asyncMethodName)
        {
            SyncMethodName = syncMethodName;
            AsyncMethodName = asyncMethodName;
        }
        
        public static implicit operator SyncAsyncMethodPair(Tuple<string, string> tuple)
        {
            return new SyncAsyncMethodPair(tuple.Item1, tuple.Item2);
        }
        
        public static implicit operator SyncAsyncMethodPair((string, string) tuple)
        {
            return new SyncAsyncMethodPair(tuple.Item1, tuple.Item2);
        }

        public string ReplaceName(string oldName)
        {
            var commonPrefixLength = SyncMethodName.LastIndexOf(oldName, StringComparison.InvariantCulture);
            var commonSuffixStart = commonPrefixLength + oldName.Length;
            var commonSuffix = SyncMethodName.Substring(commonSuffixStart);
            var asyncNameEnd = AsyncMethodName.LastIndexOf(commonSuffix, StringComparison.InvariantCulture);
           
            return AsyncMethodName.Substring(commonPrefixLength, asyncNameEnd - commonPrefixLength );
        }

        public bool MatchSyncMethod(ISymbol methodSymbol)
        {
            return methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == SyncMethodName;
        }
    }
}
