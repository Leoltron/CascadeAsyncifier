using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysisApp.Utils
{
    public static class CompilationUnitSyntaxExtensions
    {
        public static CompilationUnitSyntax WithTasksUsingDirective(this CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit.Usings.Any(u => u.Name.ToString() == "System.Threading.Tasks"))
                return compilationUnit;

            return compilationUnit.AddUsings(ExtendedSyntaxFactory.UsingDirective("System", "Threading", "Tasks"));
        }
    }
}
