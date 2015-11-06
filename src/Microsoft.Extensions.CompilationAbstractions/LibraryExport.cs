using System.Collections.Generic;

namespace Microsoft.Extensions.CompilationAbstractions
{
    public class LibraryExport
    {
        public static readonly LibraryExport Empty = new LibraryExport();

        private LibraryExport() : this(null, null) { }
        public LibraryExport(IMetadataReference metadataReference) : this(new List<IMetadataReference>() { metadataReference }) { }
        public LibraryExport(IList<IMetadataReference> metadataReferences) : this(metadataReferences, null) { }
        public LibraryExport(IList<ISourceReference> sourceReferences) : this(null, sourceReferences) { }
        public LibraryExport(IList<IMetadataReference> metadataReferences, IList<ISourceReference> sourceReferences)
        {
            MetadataReferences = metadataReferences ?? new List<IMetadataReference>();
            SourceReferences = sourceReferences ?? new List<ISourceReference>();
        }

        public IList<IMetadataReference> MetadataReferences { get; }
        public IList<ISourceReference> SourceReferences { get; }
    }
}