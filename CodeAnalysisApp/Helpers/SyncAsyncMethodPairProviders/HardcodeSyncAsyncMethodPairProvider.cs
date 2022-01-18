using System.Collections.Generic;

namespace CodeAnalysisApp.Helpers.SyncAsyncMethodPairProviders
{
    public class HardcodeSyncAsyncMethodPairProvider : ISyncAsyncMethodPairProvider
    {
        public IEnumerable<SyncAsyncMethodPair> Provide()
        {
            return new List<SyncAsyncMethodPair>
            {
                ("System.Threading.Tasks.Task.WaitAll(params System.Threading.Tasks.Task[])", "System.Threading.Tasks.Task.WhenAll(params System.Threading.Tasks.Task[])"),
                ("System.Threading.Tasks.Task.WaitAny(params System.Threading.Tasks.Task[])", "System.Threading.Tasks.Task.WhenAny(params System.Threading.Tasks.Task[])"),
                
                ("System.Net.WebClient.DownloadData(string)", "System.Net.WebClient.DownloadDataTaskAsync(string)"),
                ("System.Net.WebClient.DownloadFile(string, string)", "System.Net.WebClient.DownloadFileTaskAsync(string, string)"),
                ("System.Net.WebClient.DownloadString(string)", "System.Net.WebClient.DownloadStringTaskAsync(string)"),
                ("System.Net.WebClient.DownloadData(System.Uri)", "System.Net.WebClient.DownloadDataTaskAsync(System.Uri)"),
                ("System.Net.WebClient.DownloadFile(System.Uri, string)", "System.Net.WebClient.DownloadFileTaskAsync(System.Uri, string)"),
                ("System.Net.WebClient.DownloadString(System.Uri)", "System.Net.WebClient.DownloadStringTaskAsync(System.Uri)"),
            };
        }
    }
}
