using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class CompilationContext
    {
        public Project Project { get; set; }
        public CSharpCompilation Compilation { get; set; }
        public IList<Diagnostic> Diagnostics { get; set; }
        public IEnumerable<TypeCompilationContext> AssemblyNeutralTypes { get; set; }

        public CompilationContext()
        {
            Diagnostics = new List<Diagnostic>();
        }
    }
}
