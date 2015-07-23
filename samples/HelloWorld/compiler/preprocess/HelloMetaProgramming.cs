using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Roslyn;

namespace HelloWorld.compiler.preprocess
{
    public class HelloMetaProgramming : ICompileModule
    {
        private readonly IApplicationEnvironment _applicationEnvironment;

        public HelloMetaProgramming(IApplicationEnvironment applicationEnvironment)
        {
            _applicationEnvironment = applicationEnvironment;
        }

        public void BeforeCompile(IBeforeCompileContext context)
        {
            var options = CSharpParseOptions.Default
                .WithLanguageVersion(context.Compilation.LanguageVersion);
            context.Compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(@"
public class Foo 
{
    public string Message
    {
        get { return """ + $"Metaprogramming in {_applicationEnvironment.ApplicationName}" + @"!""; } 
    }
}", options));
        }

        public void AfterCompile(IAfterCompileContext context)
        {
        }
    }
}
