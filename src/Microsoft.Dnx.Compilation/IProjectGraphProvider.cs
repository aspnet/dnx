using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation
{
    /// <summary>
    /// Provides an interface to a provider that can walk the dependency graph for a specific project, within
    /// the context of a particular application.
    /// </summary>
    public interface IProjectGraphProvider
    {
        LibraryManager GetProjectGraph(Project project, FrameworkName targetFramework);
    }
}
