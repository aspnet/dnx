using System.Collections.Generic;
using Microsoft.Net.Runtime.Loader;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynDepenencyExport : DependencyExport
    {
        public RoslynDepenencyExport(
            IList<IMetadataReference> metadataReferences,
            IList<ISourceReference> sourceReferences,
            CompilationContext compilationContext)
            : base(metadataReferences, sourceReferences)
        {
            CompilationContext = compilationContext;
        }

        public CompilationContext CompilationContext { get; set; }
    }
}
