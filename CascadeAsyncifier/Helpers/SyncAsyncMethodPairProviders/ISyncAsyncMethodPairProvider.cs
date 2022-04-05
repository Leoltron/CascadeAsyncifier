using System.Collections.Generic;

namespace CascadeAsyncifier.Helpers.SyncAsyncMethodPairProviders
{
    public interface ISyncAsyncMethodPairProvider
    {
        IEnumerable<SyncAsyncMethodPair> Provide();
    }
}
