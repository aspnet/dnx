using System;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngineContext
    {
        public IServiceProvider Services { get { return _compilerServices; } }
        public IAssemblyLoadContext DefaultLoadContext { get; set; }
        public IApplicationEnvironment ApplicationEnvironment { get; set; }
        public IRuntimeEnvironment RuntimeEnvironment { get; set; }
        public CompilationCache CompilationCache { get; set; }

        private readonly ServiceProvider _compilerServices = new ServiceProvider();

        public CompilationEngineContext(IApplicationEnvironment applicationEnvironment,
                                        IRuntimeEnvironment runtimeEnvironment,
                                        IAssemblyLoadContext defaultLoadContext,
                                        CompilationCache cache)
        {
            ApplicationEnvironment = applicationEnvironment;
            RuntimeEnvironment = runtimeEnvironment;
            DefaultLoadContext = defaultLoadContext;
            CompilationCache = cache;

            // Register compiler services
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