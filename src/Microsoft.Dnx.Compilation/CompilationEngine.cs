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
        private readonly CompilationEngineContext _context;

        public CompilationEngine(CompilationEngineContext context)
        {
            _context = context;

            CompilationCache = _context.CompilationCache;
        }

        public CompilationCache CompilationCache { get; }

        public Assembly LoadProject(Project project, FrameworkName targetFramework, string aspect, IAssemblyLoadContext loadContext, AssemblyName assemblyName)
        {
            var exporter = CreateProjectExporter(project, targetFramework, _context.ApplicationEnvironment.Configuration);

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
            var libraryManager = _context.ProjectGraphProvider.GetProjectGraph(project, targetFramework);

            // Create an application host context to use to drive a Load Context used to load Precompilers
            var context = new ApplicationHostContext
            {
                Project = project,
                RuntimeIdentifiers = _context.RuntimeEnvironment.GetAllRuntimeIdentifiers(),
                TargetFramework = _context.ApplicationEnvironment.RuntimeFramework
            };

            var libraries = ApplicationHostContext.GetRuntimeLibraries(context);

            // This load context represents the graph that will be used to *load* the compiler and other
            // build time related dependencies
            var loadContext = new RuntimeLoadContext(libraries, this, _context.DefaultLoadContext);

            return new LibraryExporter(libraryManager, loadContext, this, configuration);
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
