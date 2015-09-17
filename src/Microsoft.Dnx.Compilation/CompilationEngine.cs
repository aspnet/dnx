using System;
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
        private readonly CompilationEngineContext _context;

        public CompilationEngine(CompilationEngineContext context)
        {
            _context = context;

            CompilationCache = _context.CompilationCache;
        }

        public CompilationCache CompilationCache { get; }

        public Assembly LoadProject(Project project, FrameworkName targetFramework, string aspect, IAssemblyLoadContext loadContext, AssemblyName assemblyName)
        {
            var exporter = CreateProjectExporter(_context.ApplicationEnvironment.Configuration);

            // Export the project
            var export = exporter.ExportProject(project, targetFramework, aspect);

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

        public LibraryExporter CreateProjectExporter(string configuration)
        {
            return new LibraryExporter(this, configuration);
        }

        public ProjectExportContext CreateProjectExportContext(Project project, FrameworkName targetFramework)
        {
            // This library manager represents the graph that will be used to resolve
            // references (compiler /r in csc terms)
            var compilationAppHost = new ApplicationHostContext
            {
                Project = project,
                TargetFramework = targetFramework
            };

            ApplicationHostContext.Initialize(compilationAppHost);

            // Create an application host context to use to drive a Load Context used to load Precompilers
            var runtimeAppHost = new ApplicationHostContext
            {
                Project = project,
                RuntimeIdentifiers = _context.RuntimeEnvironment.GetAllRuntimeIdentifiers(),
                TargetFramework = _context.ApplicationEnvironment.RuntimeFramework
            };

            var libraries = ApplicationHostContext.GetRuntimeLibraries(runtimeAppHost);

            // This load context represents the graph that will be used to *load* the compiler and other
            // build time related dependencies
            var loadContext = new RuntimeLoadContext(libraries, this, _context.DefaultLoadContext);

            return new ProjectExportContext
            {
                ApplicationHostContext = compilationAppHost,
                LoadContext = loadContext
            };
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
