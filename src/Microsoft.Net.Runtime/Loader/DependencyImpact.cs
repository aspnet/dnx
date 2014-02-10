using System.Collections.Generic;

namespace Microsoft.Net.Runtime.Loader
{
    public class DependencyImpact
    {
        public DependencyImpact(string path)
            : this(new MetadataFileReference(path))
        {
        }

        public DependencyImpact(IMetadataReference metadataReference)
        {
            MetadataReferences = new List<IMetadataReference>
            {
                metadataReference
            };
        }

        public DependencyImpact(IList<IMetadataReference> metadataReferences)
        {
            MetadataReferences = metadataReferences;
        }

        public IList<IMetadataReference> MetadataReferences { get; set; }
    }
}
