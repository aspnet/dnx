
namespace Microsoft.Framework.Runtime
{
    internal class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name
        {
            get;
            private set;
        }

        public string Path { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
