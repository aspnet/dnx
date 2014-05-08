using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynMetadataReference : IRoslynMetadataReference
    {
        public RoslynMetadataReference(string name, MetadataReference metadataReference)
        {
            Name = name;
            MetadataReference = metadataReference;
        }

        public string Name
        {
            get;
            private set;
        }

        public MetadataReference MetadataReference { get; private set; }

        public override string ToString()
        {
            return MetadataReference.ToString();
        }
    }
}
