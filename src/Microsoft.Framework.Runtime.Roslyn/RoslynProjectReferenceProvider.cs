using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectReferenceProvider : IProjectReferenceProvider
    {
        private readonly RoslynCompiler _compiler;

        public RoslynProjectReferenceProvider(IFileWatcher watcher)
        {
            _compiler = new RoslynCompiler(watcher);
        }

        public IMetadataProjectReference GetProjectReference(
            Project project,
            FrameworkName targetFramework,
            string configuration,
            ILibraryExport projectDependenciesExport)
        {
            var compliationContext = _compiler.CompileProject(project, targetFramework, configuration, projectDependenciesExport);

            if (compliationContext == null)
            {
                return null;
            }

            // Project reference
            return new RoslynProjectReference(compliationContext);
        }
    }
}