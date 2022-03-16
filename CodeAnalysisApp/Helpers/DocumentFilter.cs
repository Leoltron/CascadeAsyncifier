using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Helpers
{
    public class DocumentFilter
    {
        public bool IgnoreDocument(Document document) => document.Name.EndsWith("designer.cs");
    }
}
