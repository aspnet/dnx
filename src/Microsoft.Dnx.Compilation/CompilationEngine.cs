using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Extensions.CompilationAbstractions;

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

        public Assembly LoadProject(Project project, FrameworkName targetFramework, string aspect, IAssemblyLoadContext loadContext, AssemblyName assemblyName, string configuration)
        {
            var exporter = CreateProjectExporter(project, targetFramework, configuration);

            // Export the project
            var export = exporter.GetExport(project.Name, aspect);

            if (export == null)
            {
                return null;
            }

            // Load the metadata reference
            foreach (var projectReference in export.MetadataReferences.OfType<IMetadataProjectReference>())
            {
                if (string.Equals(projectReference.Name, project.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectReference.Load(assemblyName, loadContext);
                }
            }

            return null;
        }

        public LibraryExporter CreateProjectExporter(Project project, FrameworkName targetFramework, string configuration)
        {
            // This library manager represents the graph that will be used to resolve
            // references (compiler /r in csc terms)

            var context = new ApplicationHostContext
            {
                Project = project,
                TargetFramework = targetFramework
            };

            ApplicationHostContext.Initialize(context);

            return new LibraryExporter(context.LibraryManager, this, configuration);
        }

        public IAssemblyLoadContext CreateBuildLoadContext(Project project, string configuration)
        {
            // This load context represents the graph that will be used to *load* the compiler and other
            // build time related dependencies
            return new BuildLoadContext(project, this, _context, configuration);
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
