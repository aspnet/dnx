
namespace Microsoft.Net.Runtime
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

        public string Path { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
