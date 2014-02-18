namespace Microsoft.Net.Runtime.Loader
{
    public interface ISourceFileReference : ISourceReference
    {
        string Path { get; }
    }
}