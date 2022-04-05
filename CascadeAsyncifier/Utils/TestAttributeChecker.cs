using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Utils
{
    public class TestAttributeChecker
    {
        private static readonly string[] testMethodAttributeTypeNames = {
            "NUnit.Framework.TestCaseAttribute",
            "NUnit.Framework.TestAttribute",
            "NUnit.Framework.TestCaseSourceAttribute",
            "Xunit.FactAttribute",
            "Xunit.ClassDataAttribute",
            "Xunit.TheoryAttribute",
            "BenchmarkDotNet.Attributes.BenchmarkAttribute",
        };
        
        private static readonly ConditionalWeakTable<Compilation, TestAttributeChecker> instances = new();

        public static TestAttributeChecker GetInstance(Compilation compilation)
        {
            if (instances.TryGetValue(compilation, out var instance))
                return instance;

            instance = new TestAttributeChecker(compilation);
            instances.Add(compilation, instance);

            return instance;
        }

        private readonly HashSet<ITypeSymbol> testMethodAttributeTypes = new(SymbolEqualityComparer.Default);
        private TestAttributeChecker(Compilation compilation)
        {
            foreach (var attributeTypeName in testMethodAttributeTypeNames)
            {
                var type = compilation.GetTypeByMetadataName(attributeTypeName);
                if (type != null)
                {
                    testMethodAttributeTypes.Add(type);
                }
            }
        }

        public bool HasTestAttribute(IMethodSymbol method) => method.GetAttributes().Any(a => IsTestAttribute(a.AttributeClass));
        private bool IsTestAttribute(ITypeSymbol type) => testMethodAttributeTypes.Contains(type);
    }
}
