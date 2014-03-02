using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime
{
    // TODO: Fix issues with tooling and neutral interfaces
    // [AssemblyNeutral]
    public interface IRoslynMetadataReference : IMetadataReference
    {
        MetadataReference MetadataReference { get; }
    }
}
