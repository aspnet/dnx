namespace Microsoft.Net.Runtime.Loader
{
    public class SourceFileReference : ISourceFileReference
    {
        public SourceFileReference(string path)
        {
            Path = path;
        }

        public string Path { get; private set; }
    }
}