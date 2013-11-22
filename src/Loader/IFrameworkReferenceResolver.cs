using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;

namespace Loader
{
    public interface IFrameworkReferenceResolver
    {
        IEnumerable<string> GetFrameworkReferences(FrameworkName frameworkName);

        IEnumerable<MetadataReference> GetDefaultReferences(FrameworkName frameworkName);
    }
}
