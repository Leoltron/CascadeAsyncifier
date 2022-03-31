using System;
using System.Linq;
using System.Runtime.CompilerServices;
using CodeAnalysisApp.Extensions;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Helpers
{
    public static class IsTaskNameUnique
    {
        private static readonly ConditionalWeakTable<Compilation, Tuple<bool>> values = new();

        public static bool For(Compilation compilation)
        {
            if (values.TryGetValue(compilation, out var value))
                return value.Item1;

            value = Tuple.Create(!compilation.GlobalNamespace.GetAllTypes().Where(e => e.Name == "Task").Skip(1).Any());
            values.Add(compilation, value);

            return value.Item1;
        }
    }
}
