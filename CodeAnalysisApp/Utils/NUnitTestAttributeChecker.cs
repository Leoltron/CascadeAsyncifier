using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Utils
{
    public class NUnitTestAttributeChecker
    {
        private readonly HashSet<ITypeSymbol> attributes = new(SymbolEqualityComparer.Default);
        public NUnitTestAttributeChecker(Compilation compilation)
        {
            //attributes.Add(compilation.GetTypeByMetadataName("NUnit.Framework.SetUpAttribute"));
            //attributes.Add(compilation.GetTypeByMetadataName("NUnit.Framework.TearDownAttribute"));
            //attributes.Add(compilation.GetTypeByMetadataName("NUnit.Framework.OneTimeSetUpAttribute"));
            //attributes.Add(compilation.GetTypeByMetadataName("NUnit.Framework.OneTimeTearDownAttribute"));
            //attributes.Add(compilation.GetTypeByMetadataName("NUnit.Framework.TestCaseAttribute"));
            attributes.Add(compilation.GetTypeByMetadataName("NUnit.Framework.TestAttribute"));
            attributes.Add(compilation.GetTypeByMetadataName("NUnit.Framework.TestCaseSourceAttribute"));
        }

        public bool IsTestAttribute(ITypeSymbol type) => attributes.Contains(type);

        public bool HasTestAttribute(IMethodSymbol method) => method.GetAttributes().Any(a => IsTestAttribute(a.AttributeClass));
    }
}
