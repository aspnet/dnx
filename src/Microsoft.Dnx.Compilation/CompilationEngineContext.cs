using System;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngineContext
    {
        public IProjectGraphProvider ProjectGraphProvider { get; }
        public IFileWatcher FileWatcher { get; }
        public IServiceProvider Services { get { return _compilerServices; } }
        public IAssemblyLoadContext DefaultLoadContext { get; private set; }
        public IApplicationEnvironment ApplicationEnvironment { get; private set; }
        public CompilationCache CompilationCache { get; private set; }

        private readonly ServiceProvider _compilerServices = new ServiceProvider();

        public CompilationEngineContext(IApplicationEnvironment applicationEnvironment,
                                        IAssemblyLoadContext defaultLoadContext,
                                        CompilationCache cache) :
            this(applicationEnvironment, defaultLoadContext, cache, NoopWatcher.Instance)
        {

        }

        public CompilationEngineContext(IApplicationEnvironment applicationEnvironment,
                                        IAssemblyLoadContext defaultLoadContext,
                                        CompilationCache cache,
                                        IFileWatcher fileWatcher)
        {
            ApplicationEnvironment = applicationEnvironment;
            DefaultLoadContext = defaultLoadContext;
            ProjectGraphProvider = new ProjectGraphProvider();
            CompilationCache = cache;
            FileWatcher = fileWatcher;

            // Register compiler services
            AddCompilationService(typeof(IFileWatcher), FileWatcher);
            AddCompilationService(typeof(IApplicationEnvironment), ApplicationEnvironment);
            AddCompilationService(typeof(ICache), CompilationCache.Cache);
            AddCompilationService(typeof(ICacheContextAccessor), CompilationCache.CacheContextAccessor);
            AddCompilationService(typeof(INamedCacheDependencyProvider), CompilationCache.NamedCacheDependencyProvider);
        }

        public void AddCompilationService(Type type, object instance)
        {
            _compilerServices.Add(type, instance);
        }
    }
}