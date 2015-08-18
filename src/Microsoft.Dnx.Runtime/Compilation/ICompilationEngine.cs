using System;
using System.Reflection;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public interface ICompilationEngine
    {
        Assembly LoadProject(Project project, string aspect, IAssemblyLoadContext loadContext);
    }
}
