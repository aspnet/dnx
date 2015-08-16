using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Compilation;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngine : ICompilationEngine
    {
        private readonly Dictionary<TypeInformation, IProjectCompiler> _compilers = new Dictionary<TypeInformation, IProjectCompiler>();

        private readonly CompilationEngineContext _context;

        public CompilationEngine(CompilationCache compilationCache, CompilationEngineContext context)
        {
            _context = context;

            RootLibraryExporter = new LibraryExporter(_context.LibraryManager, this, _context.TargetFramework, _context.Configuration);

            CompilationCache = compilationCache;

            // TODO(anurse): Switch to project factory model to avoid needing to do this.
            _context.AddCompilationService(typeof(ICache), CompilationCache.Cache);
            _context.AddCompilationService(typeof(ICacheContextAccessor), CompilationCache.CacheContextAccessor);
            _context.AddCompilationService(typeof(INamedCacheDependencyProvider), CompilationCache.NamedCacheDependencyProvider);
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
                CompilerServices.CreateService<IProjectCompiler>(_context.Services, _context.BuildLoadContext, typeInfo));
        }
    }
}
