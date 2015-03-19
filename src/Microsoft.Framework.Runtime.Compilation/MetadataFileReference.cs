using System;

namespace Microsoft.Framework.Runtime.Compilation
{
    public class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }

        public override string ToString()
        {
            return "Metadata: " + Path;
        }
    }
}