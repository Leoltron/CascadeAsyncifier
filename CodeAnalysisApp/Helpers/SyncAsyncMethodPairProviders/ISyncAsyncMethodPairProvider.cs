using System.Collections.Generic;

namespace CodeAnalysisApp.Helpers.SyncAsyncMethodPairProviders
{
    public interface ISyncAsyncMethodPairProvider
    {
        IEnumerable<SyncAsyncMethodPair> Provide();
    }
}
