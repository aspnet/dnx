using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public class CompilationEngineContext
    {
        public LibraryManager LibraryManager { get; }
        public IProjectGraphProvider ProjectGraphProvider { get; }
        public IFileWatcher FileWatcher { get; }
        public FrameworkName TargetFramework { get; }
        public string Configuration { get; }
        public IAssemblyLoadContext BuildLoadContext { get; }

        public CompilationEngineContext(LibraryManager libraryManager, IProjectGraphProvider projectGraphProvider, IFileWatcher fileWatcher, FrameworkName targetFramework, string configuration, IAssemblyLoadContext buildLoadContext)
        {
            LibraryManager = libraryManager;
            ProjectGraphProvider = projectGraphProvider;
            FileWatcher = fileWatcher;
            TargetFramework = targetFramework;
            Configuration = configuration;
            BuildLoadContext = buildLoadContext;
        }
    }
}