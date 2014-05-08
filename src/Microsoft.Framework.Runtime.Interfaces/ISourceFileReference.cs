namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface ISourceFileReference : ISourceReference
    {
        string Path { get; }
    }
}