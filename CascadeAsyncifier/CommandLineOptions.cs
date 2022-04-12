using CommandLine;

namespace CascadeAsyncifier
{
    public class CommandLineOptions
    {
        [Option(longName: "solution-path",  HelpText = "Path to the solution. If not specified, will try to use solution file in current directory.")]
        public string SolutionPath { get; set; }
        [Option(longName: "msbuild-path",  HelpText = "Path to the MSBuild folder. If not specified, will try to detect automatically.")]
        public string MsBuildPath { get; set; }
        [Option(longName: "target-framework",  HelpText = "If solution projects target multiple frameworks, the tool needs to know which one to use. This parameter, if specified, will be passed as MSBuild argument.")]
        public string TargetFramework { get; set; }
        
        [Option(longName: "configure-await-false", HelpText = "Will add .ConfigureAwait(false) to every await call (not only to generated methods)")]
        public bool UseConfigureAwaitFalse { get; set; }
        [Option(longName: "omit-async-await", HelpText = "If async method's only await expression is its last statement (either as await task; or return await task;) " +
                                                         "then tool will remove \"async\" and \"await\" keywords, passing underlying Task expression as a return value.")]
        public bool OmitAsyncAwait { get; set; }
        
        [Option(longName: "starting-file-path-regex", HelpText = "By default, tool asyncifies every method that makes calls of methods with async overloads. " +
                                                        "If this option is specified, it will only look at methods in files matching regular expression. "+
                                                        "Any method that used previously asnycified methods will be asyncified regardless of its path.")]
        public string StartingFilePathRegex { get; set; }
    }
}
