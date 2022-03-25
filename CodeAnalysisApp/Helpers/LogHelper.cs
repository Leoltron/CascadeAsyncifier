using Microsoft.CodeAnalysis;
using Serilog;

namespace CodeAnalysisApp.Helpers
{
    public static class LogHelper
    {
        public static void ManualAsyncificationRequired(Location location, string methodWithAsyncOverload)
        {
            ManualAsyncificationRequired(location.GetLineSpan(), methodWithAsyncOverload);
        }

        public static void ManualAsyncificationRequired(FileLinePositionSpan span, string methodWithAsyncOverload)
        {
            if (!span.IsValid)
                return;

            Log.Information(
                "Cant automatically apply async overload {}in {}",
                string.IsNullOrEmpty(methodWithAsyncOverload) ? "" : $"of {methodWithAsyncOverload} ",
                span);
        }

        public static void CantAsyncifyInOutRefMethod(string methodName, FileLinePositionSpan span)
        {
            Log.Information(
                "Cant automatically convert method {} in {}: some of arguments have \"in\", \"out\", or \"ref\" modifier",
                methodName,
                span);
        }
    }
}
