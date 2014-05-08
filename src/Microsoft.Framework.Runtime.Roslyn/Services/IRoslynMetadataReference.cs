using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IRoslynMetadataReference : IMetadataReference
    {
        MetadataReference MetadataReference { get; }
    }
}
