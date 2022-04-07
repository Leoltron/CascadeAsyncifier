using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CascadeAsyncifier.Rewriters
{
    public abstract class RewriterFactory
    {
        public abstract CSharpSyntaxRewriter Build(SemanticModel model);
        
        public bool CanInit(CommandLineOptions options) => CanInitFunc?.Invoke(options) ?? true;
        
        public string Name { get; init; }
        
        public Func<CommandLineOptions, bool> CanInitFunc { private get; init; }
    }

    public class SimpleRewriterFactory<TRewriter> : RewriterFactory where TRewriter : CSharpSyntaxRewriter, new()
    {
        public SimpleRewriterFactory(string name)
        {
            Name = name;
        }

        public override CSharpSyntaxRewriter Build(SemanticModel model) => new TRewriter();

    }

    public class LambdaRewriterFactory : RewriterFactory
    {
        private readonly Func<SemanticModel, CSharpSyntaxRewriter> factory;

        public LambdaRewriterFactory(Func<SemanticModel, CSharpSyntaxRewriter> factory, string name)
        {
            this.factory = factory;
            Name = name;
        }


        public override CSharpSyntaxRewriter Build(SemanticModel model) => factory(model);
    }
}
