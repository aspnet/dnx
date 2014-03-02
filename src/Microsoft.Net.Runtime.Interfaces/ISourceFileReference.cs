namespace Microsoft.Net.Runtime
{
    [AssemblyNeutral]
    public interface ISourceFileReference : ISourceReference
    {
        string Path { get; }
    }
}