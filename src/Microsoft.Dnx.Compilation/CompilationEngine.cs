using System;
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
        private readonly CompilationEngineContext _context;

        public CompilationEngine(CompilationEngineContext context)
        {
            _context = context;

            CompilationCache = _context.CompilationCache;
        }

        public CompilationCache CompilationCache { get; }

        public Assembly LoadProject(Project project, FrameworkName targetFramework, string aspect, IAssemblyLoadContext loadContext, AssemblyName assemblyName)
        {
            var exporter = CreateExporter(_context.ApplicationEnvironment.Configuration);

            var export = exporter.ExportProject(project, targetFramework, aspect);

            return export?.ProjectReference?.Load(assemblyName, loadContext);
        }

        public LibraryExporter CreateExporter(string configuration)
        {
            return new LibraryExporter(this, configuration);
        }

        public IAssemblyLoadContext CreateBuildLoadContext(Project project)
        {
            // This load context represents the graph that will be used to *load* the compiler and other
            // build time related dependencies
            return new BuildLoadContext(project, this, _context);
        }

        public IProjectCompiler GetCompiler(TypeInformation provider, IAssemblyLoadContext loadContext)
        {
            // TODO: Optimize the default compiler case by using the default load context directly

            var services = new ServiceProvider(_context.Services);
            services.Add(typeof(IAssemblyLoadContext), loadContext);

            var assembly = loadContext.Load(provider.AssemblyName);

            var type = assembly.GetType(provider.TypeName);

            return (IProjectCompiler)ActivatorUtilities.CreateInstance(services, type);
        }
    }
}
