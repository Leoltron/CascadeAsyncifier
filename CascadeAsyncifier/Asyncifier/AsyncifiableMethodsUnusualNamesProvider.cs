using System.Collections.Generic;

namespace CascadeAsyncifier.Asyncifier
{
    public static class AsyncifiableMethodsUnusualNamesProvider
    {
        public static IEnumerable<(string typeName, string syncName, string asyncName)> Provide()
        {
            return new[]
            {
                ("System.Threading.Tasks.Task", "WaitAll", "WhenAll"),
                ("System.Threading.Tasks.Task", "WaitAny", "WhenAny"),
                ("System.Threading.Tasks.Task", "Wait", "Delay"),
                ("System.Net.WebClient", "DownloadData", "DownloadDataTaskAsync"),
                ("System.Net.WebClient", "DownloadFile", "DownloadFileTaskAsync"),
                ("System.Net.WebClient", "DownloadString", "DownloadStringTaskAsync"),
            };
        }
    }
}
