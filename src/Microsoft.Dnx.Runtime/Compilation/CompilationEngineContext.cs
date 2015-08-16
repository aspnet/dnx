using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

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
        public IServiceProvider Services { get { return _compilerServices; } }

        private readonly ServiceProvider _compilerServices = new ServiceProvider();

        public CompilationEngineContext(LibraryManager libraryManager, IProjectGraphProvider projectGraphProvider, IFileWatcher fileWatcher, FrameworkName targetFramework, string configuration, IAssemblyLoadContext buildLoadContext)
        {
            LibraryManager = libraryManager;
            ProjectGraphProvider = projectGraphProvider;
            FileWatcher = fileWatcher;
            TargetFramework = targetFramework;
            Configuration = configuration;
            BuildLoadContext = buildLoadContext;

            // Register compiler services
            AddCompilationService(typeof(IFileWatcher), fileWatcher);
            AddCompilationService(typeof(IAssemblyLoadContext), buildLoadContext);
        }

        public void AddCompilationService(Type type, object instance)
        {
            _compilerServices.Add(type, instance);
        }
    }
}