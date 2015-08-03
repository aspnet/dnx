using System;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public interface ICompilationEngine : IDisposable
    {
        event Action<string> OnInputFileChanged;

        ILibraryExporter RootLibraryExporter { get; }

        Assembly LoadProject(Project project, FrameworkName targetFramework, string configuration, string aspect, IAssemblyLoadContext loadContext);
    }
}
