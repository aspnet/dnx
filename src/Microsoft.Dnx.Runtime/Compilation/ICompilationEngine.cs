using System;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public interface ICompilationEngine
    {
        Assembly LoadProject(Project project, FrameworkName targetFramework, string aspect, IAssemblyLoadContext loadContext, AssemblyName assemblyName);
    }
}
