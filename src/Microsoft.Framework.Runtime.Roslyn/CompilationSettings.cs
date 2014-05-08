using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationSettings
    {
        public IEnumerable<string> Defines { get; set; }
        public CSharpCompilationOptions CompilationOptions { get; set; }
    }
}
