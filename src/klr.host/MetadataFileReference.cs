using Microsoft.Net.Runtime;

namespace klr.host
{
    internal class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string path)
        {
            Path = path;
        }

        public string Path
        {
            get;
            private set;
        }
    }
}
