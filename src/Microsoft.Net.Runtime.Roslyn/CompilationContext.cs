using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class CompilationContext
    {
        /// <summary>
        /// The project associated with this compilation
        /// </summary>
        public Project Project { get; set; }

        // Processed information
        public CSharpCompilation Compilation { get; set; }
        public IList<Diagnostic> Diagnostics { get; set; }
        public IList<AssemblyNeutralMetadataReference> AssemblyNeutralReferences { get; set; }
        public IList<CompilationContext> ProjectReferences { get; set; }

        public CompilationContext()
        {
            Diagnostics = new List<Diagnostic>();
        }
    }
}
