using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public class CompilationEngineContext
    {
        public IProjectGraphProvider ProjectGraphProvider { get; }
        public IFileWatcher FileWatcher { get; }
        public IServiceProvider Services { get { return _compilerServices; } }
        public IAssemblyLoadContext DefaultLoadContext { get; private set; }
        public IApplicationEnvironment ApplicationEnvironment { get; private set; }

        private readonly ServiceProvider _compilerServices = new ServiceProvider();

        public CompilationEngineContext(IApplicationEnvironment applicationEnvironment,
                                        IAssemblyLoadContext defaultLoadContext) : 
            this(applicationEnvironment, defaultLoadContext, NoopWatcher.Instance)
        {

        }

        public CompilationEngineContext(IApplicationEnvironment applicationEnvironment, 
                                        IAssemblyLoadContext defaultLoadContext, 
                                        IFileWatcher fileWatcher)
        {
            ApplicationEnvironment = applicationEnvironment;
            DefaultLoadContext = defaultLoadContext;
            ProjectGraphProvider = new ProjectGraphProvider();
            FileWatcher = fileWatcher;

            // Register compiler services
            AddCompilationService(typeof(IFileWatcher), FileWatcher);
            AddCompilationService(typeof(IApplicationEnvironment), ApplicationEnvironment);
        }

        public void AddCompilationService(Type type, object instance)
        {
            _compilerServices.Add(type, instance);
        }
    }
}