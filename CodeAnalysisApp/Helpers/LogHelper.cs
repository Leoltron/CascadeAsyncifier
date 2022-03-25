using System;
using Microsoft.CodeAnalysis;

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
            if(!span.IsValid)
                return;
            
            Console.WriteLine(
                $"Cant automatically apply async overload {(string.IsNullOrEmpty(methodWithAsyncOverload) ? "" : $"of {methodWithAsyncOverload} ")}in {span}");
        }

        public static void CantAsyncifyInOutRefMethod(string methodName, FileLinePositionSpan span)
        {
            Console.WriteLine($"Cant automatically convert method {methodName} in {span}: some of arguments have \"in\", \"out\", or \"ref\" modifier.");
        }
    }
}
