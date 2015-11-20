using System;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public interface ICompilationEngine
    {
        Assembly LoadProject(Project project, FrameworkName targetFramework, string aspect, IAssemblyLoadContext loadContext, AssemblyName assemblyName, string configuration);
    }
}
