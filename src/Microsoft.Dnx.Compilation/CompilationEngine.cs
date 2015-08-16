using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngine : ICompilationEngine
    {
        private readonly Dictionary<TypeInformation, IProjectCompiler> _compilers = new Dictionary<TypeInformation, IProjectCompiler>();

        private readonly CompilationEngineContext _context;
        private readonly ServiceProvider _compilerServices = new ServiceProvider();

        public CompilationEngine(CompilationCache compilationCache, CompilationEngineContext context)
        {
            _context = context;

            RootLibraryExporter = new LibraryExporter(_context.LibraryManager, this, _context.TargetFramework, _context.Configuration);

            CompilationCache = compilationCache;

            // Register compiler services
            // TODO(anurse): Switch to project factory model to avoid needing to do this.
            _compilerServices.Add(typeof(ICache), CompilationCache.Cache);
            _compilerServices.Add(typeof(ICacheContextAccessor), CompilationCache.CacheContextAccessor);
            _compilerServices.Add(typeof(INamedCacheDependencyProvider), CompilationCache.NamedCacheDependencyProvider);
            _compilerServices.Add(typeof(IFileWatcher), context.FileWatcher);
            _compilerServices.Add(typeof(IAssemblyLoadContext), context.BuildLoadContext);
        }

        public CompilationCache CompilationCache { get; }

        public ILibraryExporter RootLibraryExporter { get; }

        public Assembly LoadProject(Project project, string aspect, IAssemblyLoadContext loadContext)
        {
            // Export the project
            var export = ProjectExporter.ExportProject(project, this, aspect, _context.TargetFramework, _context.Configuration);

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
            var manager = _context.ProjectGraphProvider.GetProjectGraph(project, targetFramework);
            return new LibraryExporter(manager, this, targetFramework, configuration);
        }

        public IProjectCompiler GetCompiler(TypeInformation provider)
        {
            // Load the factory
            return _compilers.GetOrAdd(provider, typeInfo =>
                CompilerServices.CreateService<IProjectCompiler>(_compilerServices, _context.BuildLoadContext, typeInfo));
        }
    }
}
