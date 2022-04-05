using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Helpers
{
    public class DocumentFilter
    {
        public bool IgnoreDocument(Document document) => document.Name.EndsWith("designer.cs");
    }
}
