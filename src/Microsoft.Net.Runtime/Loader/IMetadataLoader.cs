using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IMetadataLoader : IAssemblyLoader
    {
        MetadataReference GetMetadata(string name);
    }
}
