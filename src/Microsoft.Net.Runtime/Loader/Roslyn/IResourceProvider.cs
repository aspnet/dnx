using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Loader.Roslyn
{
    public interface IResourceProvider
    {
        IList<ResourceDescription> GetResources(string projectName, string projectPath);
    }
}
