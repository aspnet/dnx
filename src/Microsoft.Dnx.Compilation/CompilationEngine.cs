using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngine : ICompilationEngine
    {
        private readonly Dictionary<TypeInformation, IProjectCompiler> _compilers = new Dictionary<TypeInformation, IProjectCompiler>();

        private readonly Lazy<IAssemblyLoadContext> _compilerLoadContext;
        private readonly CompilationEngineContext _context;

        public CompilationEngine(
            CompilationCache compilationCache, 
            IFileWatcher fileWatcher,
            CompilationEngineContext context)
        {
            _context = context;
            RootLibraryExporter = new LibraryExporter(_context.LibraryManager, this, _context.TargetFramework, _context.Configuration);
            _compilerLoadContext = new Lazy<IAssemblyLoadContext>(() =>
            {
                var factory = (IAssemblyLoadContextFactory)_context.Services.GetService(typeof(IAssemblyLoadContextFactory));
                return factory.Create(_context.Services);
            });

            CompilationCache = compilationCache;
            FileWatcher = fileWatcher;

            // Hook up file watcher
            fileWatcher.OnChanged += s => {
                var handler = OnInputFileChanged;
                handler?.Invoke(s);
            };

            // Register compiler services
            // TODO(anurse): Switch to project factory model to avoid needing to do this.
            _context.AddService(typeof(ICache), CompilationCache.Cache);
            _context.AddService(typeof(ICacheContextAccessor), CompilationCache.CacheContextAccessor);
            _context.AddService(typeof(INamedCacheDependencyProvider), CompilationCache.NamedCacheDependencyProvider);
            _context.AddService(typeof(IFileWatcher), fileWatcher);
        }

        public CompilationCache CompilationCache { get; }

        public IFileWatcher FileWatcher { get; }

        public LibraryExporter RootLibraryExporter { get; }

        public event Action<string> OnInputFileChanged;

        public void Dispose()
        {
            throw new NotImplementedException();
        }
        
        public Assembly LoadProject(Project project, FrameworkName targetFramework, string configuration, string aspect, IAssemblyLoadContext loadContext)
        {
            // Export the project
            var export = ProjectExporter.ExportProject(project, this, aspect, targetFramework, configuration);

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
            var graph = _context.ProjectGraphProvider.GetProjectGraph(project, targetFramework, configuration);
            var manager = new LibraryManager(graph);
            return new LibraryExporter(manager, this, targetFramework, configuration);
        }

        public IProjectCompiler GetCompiler(TypeInformation provider)
        {
            // Load the factory
            return _compilers.GetOrAdd(provider, typeInfo =>
                CompilerServices.CreateService<IProjectCompiler>(_context.Services, _compilerLoadContext.Value, typeInfo));
        }
    }
}
