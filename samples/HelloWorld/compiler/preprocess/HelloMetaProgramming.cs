using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Roslyn;

namespace HelloWorld.Compiler.Preprocess
{
    public class HelloMetaProgramming : ICompileModule
    {
        public HelloMetaProgramming(IServiceProvider services)
        {
        }

        public void BeforeCompile(IBeforeCompileContext context)
        {
            // TODO: Enable
            var options = CSharpParseOptions.Default
                .WithLanguageVersion(context.Compilation.LanguageVersion);
            context.Compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(@"
public class Foo 
{
    public string Message
    {
        get { return ""Metaprogrammg!""; } 
    }
}", options));
        }

        public void AfterCompile(IAfterCompileContext context)
        {

        }
    }
}