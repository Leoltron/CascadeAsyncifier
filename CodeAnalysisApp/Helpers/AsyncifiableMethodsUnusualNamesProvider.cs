using System.Collections.Generic;

namespace CodeAnalysisApp.Helpers
{
    public class AsyncifiableMethodsUnusualNamesProvider
    {
        public static IEnumerable<(string typeName, string syncName, string asyncName)> Provide()
        {
            return new[]
            {
                ("System.Threading.Tasks.Task", "WaitAll", "WhenAll"),
                ("System.Threading.Tasks.Task", "WaitAny", "WhenAny"),
                ("System.Threading.Tasks.Task", "Wait", "Delay"),
            };
        }
    }
}
