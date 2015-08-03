using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Provides an interface to a provider that can walk the dependency graph for a specific project, within
    /// the context of a particular application.
    /// </summary>
    public interface IProjectGraphProvider
    {
        IEnumerable<LibraryDescription> GetProjectGraph(Project project, FrameworkName targetFramework, string configuration);
    }
}
