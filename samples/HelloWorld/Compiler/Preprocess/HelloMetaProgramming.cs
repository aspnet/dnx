using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;

namespace HelloWorld.Compiler.Preprocess
{
    public class HelloMetaProgramming : ICompileModule
    {
        public HelloMetaProgramming(IServiceProvider services)
        {
        }

        public void BeforeCompile(IBeforeCompileContext context)
        {
            var options = CSharpParseOptions.Default
                .WithLanguageVersion(context.CSharpCompilation.LanguageVersion);
            context.CSharpCompilation = context.CSharpCompilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(@"
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

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public class AssemblyNeutralAttribute : Attribute
    {
    }

    /// <summary>
    /// Summary description for ICompileModule
    /// </summary>
    [AssemblyNeutral]
    public interface ICompileModule
    {
        void BeforeCompile(IBeforeCompileContext context);

        void AfterCompile(IAfterCompileContext context);
    }

    [AssemblyNeutral]
    public interface IBeforeCompileContext
    {
        CSharpCompilation CSharpCompilation { get; set; }

        IList<ResourceDescription> Resources { get; }

        IList<Diagnostic> Diagnostics { get; }
    }

    [AssemblyNeutral]
    public interface IAfterCompileContext
    {
        CSharpCompilation CSharpCompilation { get; set; }

        IList<Diagnostic> Diagnostics { get; }
    }
}
