using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynLibraryExport : LibraryExport
    {
        public RoslynLibraryExport(
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
