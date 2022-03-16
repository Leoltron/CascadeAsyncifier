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
    }
}
