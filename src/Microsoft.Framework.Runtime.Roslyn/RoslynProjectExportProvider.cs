using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// Summary description for RoslynLibraryExportProvider
    /// </summary>
    public class RoslynProjectExportProvider : IProjectExportProvider
    {
        private readonly Dictionary<string, CompilationContext> _compilationCache = new Dictionary<string, CompilationContext>();
        private readonly RoslynCompiler _compiler;

        public RoslynProjectExportProvider(IFileWatcher watcher)
        {
            _compiler = new RoslynCompiler(watcher);
        }

        public ILibraryExport GetProjectExport(
            Project project,
            FrameworkName targetFramework,
            string configuration,
            ILibraryExport projectExport)
        {
            var compliationContext = GetCompilationContext(project, targetFramework, configuration, projectExport);

            if (compliationContext == null)
            {
                return null;
            }

            return compliationContext.GetLibraryExport();
        }

        private CompilationContext GetCompilationContext(
            Project project,
            FrameworkName targetFramework,
            string configuration,
            ILibraryExport projectExport)
        {
            string name = project.Name;

            CompilationContext compilationContext;
            if (_compilationCache.TryGetValue(name, out compilationContext))
            {
                return compilationContext;
            }

            var context = _compiler.CompileProject(project, targetFramework, configuration, projectExport);

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