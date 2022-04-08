using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CascadeAsyncifier.Extensions
{
    public static class CompilationUnitSyntaxExtensions
    {
        public static CompilationUnitSyntax WithTasksUsingDirective(this CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit.Usings.Any(u => u.Name.ToString() == "System.Threading.Tasks"))
                return compilationUnit;

            return compilationUnit.AddUsings(ExtendedSyntaxFactory.UsingDirective("System", "Threading", "Tasks"));
        }
        public static CompilationUnitSyntax WithUsingDirectives(this CompilationUnitSyntax compilationUnit, params string[] usingFullNames) => 
            compilationUnit.AddUsings(usingFullNames.Select(n => ExtendedSyntaxFactory.UsingDirective(n.Split('.'))).ToArray());
    }
}
