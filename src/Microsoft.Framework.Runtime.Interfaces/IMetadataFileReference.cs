
namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IMetadataFileReference : IMetadataReference
    {
        string Path { get; }
    }
}
