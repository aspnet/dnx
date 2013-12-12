using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime
{
    public interface IFrameworkReferenceResolver
    {
        IEnumerable<string> GetFrameworkReferences(FrameworkName frameworkName);

        IEnumerable<MetadataReference> GetDefaultReferences(FrameworkName frameworkName);
    }
}
