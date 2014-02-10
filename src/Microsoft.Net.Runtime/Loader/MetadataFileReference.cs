
namespace Microsoft.Net.Runtime.Loader
{
    internal class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string path)
        {
            Path = path;
        }

        public string Path { get; private set; }

        public override string ToString()
        {
            return Path;
        }
    }
}
