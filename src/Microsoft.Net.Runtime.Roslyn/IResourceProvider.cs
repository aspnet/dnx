using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    public interface IResourceProvider
    {
        IList<ResourceDescription> GetResources(Project project);
    }
}
