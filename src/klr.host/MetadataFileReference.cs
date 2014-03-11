using Microsoft.Net.Runtime;

namespace klr.host
{
    internal class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string path)
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
            Path = path;
        }

        public string Name
        {
            get;
            private set;
        }

        public string Path
        {
            get;
            private set;
        }
    }
}
