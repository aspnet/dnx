using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public class CompilationEngineContext
    {
        private ServiceProvider _services;

        public LibraryManager LibraryManager { get; }
        public IProjectGraphProvider ProjectGraphProvider { get; }
        public FrameworkName TargetFramework { get; }
        public string Configuration { get; }
        public IServiceProvider Services { get { return _services; } }

        public CompilationEngineContext(LibraryManager libraryManager, IProjectGraphProvider projectGraphProvider, IServiceProvider services, FrameworkName targetFramework, string configuration)
        {
            LibraryManager = libraryManager;
            ProjectGraphProvider = projectGraphProvider;
            TargetFramework = targetFramework;
            Configuration = configuration;

            _services = new ServiceProvider(services);
        }

        public void AddService(Type type, object instance)
        {
            _services.Add(type, instance);
        }
    }
}