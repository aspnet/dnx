using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;

namespace Loader
{
    public interface IFrameworkReferenceResolver
    {
        IEnumerable<MetadataReference> GetFrameworkReferences(string frameworkName);
    }
}
