using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.FileSystem;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader, IDependencyExporter
    {
        private readonly Dictionary<string, CompilationContext> _compilationCache = new Dictionary<string, CompilationContext>();

        private readonly IRoslynCompiler _compiler;
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly IProjectResolver _projectResolver;
        private readonly IResourceProvider _resourceProvider;

        public RoslynAssemblyLoader(IAssemblyLoaderEngine loaderEngine,
                                    IFileWatcher watcher,
                                    IProjectResolver projectResolver,
                                    IDependencyExporter dependencyExporter,
                                    IGlobalAssemblyCache globalAssemblyCache)
        {
            _loaderEngine = loaderEngine;
            _projectResolver = projectResolver;

            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();

            _resourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });
            _compiler = new RoslynCompiler(projectResolver,
                                           watcher,
                                           dependencyExporter);
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            var compilationContext = GetCompilationContext(loadContext.AssemblyName, loadContext.TargetFramework);

            if (compilationContext == null)
            {
                return null;
            }

            var project = compilationContext.Project;
            var path = project.ProjectDirectory;
            var name = project.Name;

            var resources = _resourceProvider.GetResources(project);

            compilationContext.PopulateAllAssemblyNeutralResources(resources);

            return CompileInMemory(name, compilationContext, resources);
        }

        public IDependencyExport GetDependencyExport(string name, FrameworkName targetFramework)
        {
            var compliationContext = GetCompilationContext(name, targetFramework);

            if (compliationContext == null)
            {
                return null;
            }

            return RoslynCompiler.MakeDependencyExport(compliationContext);
        }

        private CompilationContext GetCompilationContext(string name, FrameworkName targetFramework)
        {
            CompilationContext compilationContext;
            if (_compilationCache.TryGetValue(name, out compilationContext))
            {
                return compilationContext;
            }

            var context = _compiler.CompileProject(name, targetFramework);

            if (context == null)
            {
                return null;
            }

            CacheCompilation(context);

            return context;
        }

        private void CacheCompilation(CompilationContext context)
        {
            _compilationCache[context.Project.Name] = context;

            foreach (var ctx in context.ProjectReferences)
            {
                CacheCompilation(ctx);
            }
        }

        private AssemblyLoadResult CompileInMemory(string name, CompilationContext compilationContext, IEnumerable<ResourceDescription> resources)
        {
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                EmitResult result = compilationContext.Compilation.Emit(assemblyStream, pdbStream: pdbStream, manifestResources: resources);

                if (!result.Success)
                {
                    return ReportCompilationError(
                        compilationContext.Diagnostics.Where(IsError).Concat(result.Diagnostics));
                }

                var errors = compilationContext.Diagnostics.Where(IsError);
                if (errors.Any())
                {
                    return ReportCompilationError(errors);
                }

                var assemblyBytes = assemblyStream.ToArray();
                byte[] pdbBytes = pdbStream.ToArray();

                var assembly = _loaderEngine.LoadBytes(assemblyBytes, pdbBytes);

                return new AssemblyLoadResult(assembly);
            }
        }

        private static AssemblyLoadResult ReportCompilationError(IEnumerable<Diagnostic> results)
        {
            return new AssemblyLoadResult(GetErrors(results));
        }

        private static IList<string> GetErrors(IEnumerable<Diagnostic> diagnostis)
        {
            var formatter = new DiagnosticFormatter();

            return diagnostis.Select(d => formatter.Format(d)).ToList();
        }

        private static bool IsError(Diagnostic diagnostic)
        {
            return diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.IsWarningAsError;
        }
    }
}
