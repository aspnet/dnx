using System.Collections.Generic;

namespace Microsoft.Net.Runtime
{
    public class LibraryExport : ILibraryExport
    {
        public LibraryExport(string path)
            : this(new MetadataFileReference(path))
        {
        }

        public LibraryExport(IMetadataReference metadataReference)
        {
            MetadataReferences = new List<IMetadataReference>
            {
                metadataReference
            };

            SourceReferences = new List<ISourceReference>();
        }

        public LibraryExport(
            IList<IMetadataReference> metadataReferences,
            IList<ISourceReference> sourceReferences)
        {
            MetadataReferences = metadataReferences;
            SourceReferences = sourceReferences;
        }

        public IList<IMetadataReference> MetadataReferences { get; set; }

        public IList<ISourceReference> SourceReferences { get; set; }
    }
}
