using System.Text.RegularExpressions;

namespace CodeAnalysisApp.Extensions
{
    public static class StringExtensions
    {
        private static readonly Regex whitespaces = new("\\s+", RegexOptions.Compiled);

        public static bool EqualsIgnoreWhitespace(this string x, string y) => whitespaces.Replace(x, string.Empty) == whitespaces.Replace(y, string.Empty);

        // ReSharper disable once StringIsNullOrEmpty
        public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s);
        // ReSharper disable once StringIsNullOrWhiteSpace
        public static bool IsNullOrWhiteSpace(this string s) => string.IsNullOrWhiteSpace(s);
    }
}
