using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Loader;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngine : ICompilationEngine
    {
        private readonly Dictionary<TypeInformation, IProjectCompiler> _compilers = new Dictionary<TypeInformation, IProjectCompiler>();

        private readonly CompilationEngineContext _context;

        public CompilationEngine(CompilationCache compilationCache, CompilationEngineContext context)
        {
            _context = context;

            CompilationCache = compilationCache;

            // TODO(anurse): Switch to project factory model to avoid needing to do this.
            _context.AddCompilationService(typeof(ICache), CompilationCache.Cache);
            _context.AddCompilationService(typeof(ICacheContextAccessor), CompilationCache.CacheContextAccessor);
            _context.AddCompilationService(typeof(INamedCacheDependencyProvider), CompilationCache.NamedCacheDependencyProvider);
        }

        public CompilationCache CompilationCache { get; }

        public Assembly LoadProject(Project project, string aspect, IAssemblyLoadContext loadContext)
        {
            var exporter = CreateProjectExporter(project, _context.ApplicationEnvironment.RuntimeFramework, _context.ApplicationEnvironment.Configuration);

            // Export the project
            var export = exporter.GetExport(project.Name, aspect);

            // Load the metadata reference
            foreach (var projectReference in export.MetadataReferences.OfType<IMetadataProjectReference>())
            {
                if (string.Equals(projectReference.Name, project.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectReference.Load(loadContext);
                }
            }

            return null;
        }

        public LibraryExporter CreateProjectExporter(Project project, FrameworkName targetFramework, string configuration)
        {
            var libraryManager = _context.ProjectGraphProvider.GetProjectGraph(project, targetFramework);

            var runtimeLibraryManager = targetFramework != _context.ApplicationEnvironment.RuntimeFramework ? _context.ProjectGraphProvider.GetProjectGraph(project, _context.ApplicationEnvironment.RuntimeFramework) : libraryManager;

            var loadContext = new RuntimeLoadContext(runtimeLibraryManager, this, _context.DefaultLoadContext);

            return new LibraryExporter(libraryManager, loadContext, this, targetFramework, configuration);
        }

        public IProjectCompiler GetCompiler(TypeInformation provider, IAssemblyLoadContext loadContext)
        {
            var services = new ServiceProvider(_context.Services);
            services.Add(typeof(IAssemblyLoadContext), loadContext);

            // Load the factory
            return _compilers.GetOrAdd(provider, typeInfo =>
                CompilerServices.CreateService<IProjectCompiler>(services, loadContext, typeInfo));
        }
    }
}
