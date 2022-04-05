using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CascadeAsyncifier.Extensions
{
    public static class SolutionExtensions
    {
        public static Task<IEnumerable<ISymbol>> FindOverridesAsync(this Solution solution, ISymbol symbol) => 
            SymbolFinder.FindOverridesAsync(symbol, solution);
        
        public static Task<IEnumerable<ISymbol>> FindImplementationsAsync(this Solution solution, ISymbol symbol) => 
            SymbolFinder.FindImplementationsAsync(symbol, solution);


        public static async Task<IEnumerable<ISymbol>> FindOverridesAndImplementationsAsync(
            this Solution solution, ISymbol symbol)
        {
            var tasks = await Task.WhenAll(solution.FindOverridesAsync(symbol), solution.FindImplementationsAsync(symbol));
            return tasks[0].Concat(tasks[1]);
        }
    }
}
