using CodeAnalysisApp.Utils;
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

            Log.Verbose(
                "Cant automatically apply async overload {MethodWithAsyncOverload}in {LocationSpan}",
                methodWithAsyncOverload.IsNullOrEmpty() ? "" : $"of {methodWithAsyncOverload} ",
                span);
        }

        public static void CantAsyncifyInOutRefMethod(string methodName, FileLinePositionSpan span)
        {
            Log.Warning(
                "Cant automatically convert method {Method} in {Location}: some of arguments have \"in\", \"out\", or \"ref\" modifier",
                methodName,
                span);
        }
    }
}
