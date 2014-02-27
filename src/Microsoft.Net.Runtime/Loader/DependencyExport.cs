using System.Collections.Generic;

namespace Microsoft.Net.Runtime.Loader
{
    public class DependencyExport : IDependencyExport
    {
        public DependencyExport(string path)
            : this(new MetadataFileReference(path))
        {
        }

        public DependencyExport(IMetadataReference metadataReference)
        {
            MetadataReferences = new List<IMetadataReference>
            {
                metadataReference
            };
            SourceReferences = new List<ISourceReference>();
        }

        public DependencyExport(
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
