using System.Collections.Generic;
using System.Linq;

namespace CascadeAsyncifier.Rewriters
{
    public static class RewritersInfo
    {
        private static readonly RewriterFactory[] rewriters = {
            new SimpleRewriterFactory<AsyncVoidRewriter>("Async void"),
            new LambdaRewriterFactory(m => new UnawaitedInAsyncMethodCallRewriter(m), "async call without \"await\""),
            new LambdaRewriterFactory(m => new BlockingAwaitingRewriter(m), "blocking awaiting"),
            new LambdaRewriterFactory(m => new ConfigureAwaitRewriter(m), "ConfigureAwait()")
            {
                CanInitFunc = o => o.UseConfigureAwaitFalse
            },
            new LambdaRewriterFactory(m => new OnlyAwaitInReturnAsyncMethodRewriter(m), "Only one await in return")
            {
                CanInitFunc = o => o.OmitAsyncAwait
            },
            new LambdaRewriterFactory(m => new OnlyAwaitInAsyncLambdaRewriter(m), "One statement in lambda")
            {
                CanInitFunc = o => o.OmitAsyncAwait
            },
            new LambdaRewriterFactory(m => new AsyncMethodEndsWithAwaitExpressionRewriter(m), "Only one await at the end of method")
            {
                CanInitFunc = o => o.OmitAsyncAwait
            },
        };

        public static IEnumerable<RewriterFactory> GetRewriters(CommandLineOptions options) =>
            rewriters.Where(e => e.CanInit(options));
    }
}
