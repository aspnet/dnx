using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Compilation.CSharp;

namespace A.compiler.preprocess
{
    public class CompileModule : ICompileModule
    {
        public void BeforeCompile(BeforeCompileContext context)
        {
            context.Compilation = context.Compilation.AddSyntaxTrees(
                SyntaxFactory.ParseSyntaxTree(@"
namespace " + context.ProjectContext.Name + @"
{
    public class Foo
    {
        public void Bar()
        {
            System.Console.WriteLine(""Hello from generated code"");
        }
    }
}
")
            );
        }

        public void AfterCompile(AfterCompileContext context)
        {
        }
    }
}
