using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// Summary description for RoslynLibraryExportProvider
    /// </summary>
    public class RoslynLibraryExportProvider : ILibraryExportProvider
    {
        private readonly Dictionary<string, CompilationContext> _compilationCache = new Dictionary<string, CompilationContext>();
        private readonly RoslynCompiler _compiler;

        public RoslynLibraryExportProvider(IProjectResolver projectResolver,
                                           IFileWatcher watcher,
                                           ILibraryExportProvider dependencyExporter)
        {
            _compiler = new RoslynCompiler(projectResolver,
                                           watcher,
                                           dependencyExporter);
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
        {
            var compliationContext = GetCompilationContext(name, targetFramework, configuration);

            if (compliationContext == null)
            {
                return null;
            }

            return compliationContext.GetLibraryExport();
        }

        private CompilationContext GetCompilationContext(string name, FrameworkName targetFramework, string configuration)
        {
            CompilationContext compilationContext;
            if (_compilationCache.TryGetValue(name, out compilationContext))
            {
                return compilationContext;
            }

            var context = _compiler.CompileProject(name, targetFramework, configuration);

            if (context == null)
            {
                return null;
            }

            _compilationCache[name] = context;

            // This has the closure of references
            foreach (var projectReference in context.MetadataReferences.OfType<RoslynProjectReference>())
            {
                _compilationCache[projectReference.Name] = projectReference.CompilationContext;
            }
            
            return context;
        }
    }
}