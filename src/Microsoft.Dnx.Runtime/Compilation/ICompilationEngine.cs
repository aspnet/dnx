using System;
using System.Reflection;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public interface ICompilationEngine
    {
        ILibraryExporter RootLibraryExporter { get; }

        Assembly LoadProject(Project project, string aspect, IAssemblyLoadContext loadContext);
    }
}
