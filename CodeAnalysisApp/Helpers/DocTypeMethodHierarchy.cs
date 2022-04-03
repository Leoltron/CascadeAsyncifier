using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Helpers
{
    public class DocTypeMethodHierarchy
    {
        public Dictionary<TypeSyntaxSemanticPair, List<MethodSyntaxSemanticPair>> TypeToMethods { get; }
        public Dictionary<Document, List<TypeSyntaxSemanticPair>> DocsToTypes { get; }

        public DocTypeMethodHierarchy(Dictionary<TypeSyntaxSemanticPair, List<MethodSyntaxSemanticPair>> typeToMethods,
                                      Dictionary<Document, List<TypeSyntaxSemanticPair>> docsToTypes)
        {
            TypeToMethods = typeToMethods;
            DocsToTypes = docsToTypes;
        }
    }
}
