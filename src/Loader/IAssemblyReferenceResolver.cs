using Microsoft.CodeAnalysis;

namespace Loader
{
    public interface IAssemblyReferenceResolver
    {
        MetadataReference ResolveReference(string name);
    }
}
