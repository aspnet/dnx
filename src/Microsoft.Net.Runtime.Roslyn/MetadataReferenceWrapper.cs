using Microsoft.CodeAnalysis;
using Microsoft.Net.Runtime.Loader;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class MetadataReferenceWrapper : IRoslynMetadataReference
    {
        public MetadataReferenceWrapper(MetadataReference metadataReference)
        {
            MetadataReference = metadataReference;
        }

        public MetadataReference MetadataReference { get; private set; }

        public override string ToString()
        {
            return MetadataReference.ToString();
        }
    }
}
