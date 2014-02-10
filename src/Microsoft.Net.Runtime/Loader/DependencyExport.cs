using System.Collections.Generic;

namespace Microsoft.Net.Runtime.Loader
{
    public class DependencyExport
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
        }

        public DependencyExport(IList<IMetadataReference> metadataReferences)
        {
            MetadataReferences = metadataReferences;
        }

        public IList<IMetadataReference> MetadataReferences { get; set; }
    }
}
