using System;
using System.Collections.Concurrent;
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
        private readonly RoslynCompiler _compiler;

        public RoslynProjectExportProvider(IFileWatcher watcher)
        {
            _compiler = new RoslynCompiler(watcher);
        }

        public ILibraryExport GetProjectExport(
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

            var metadataReferences = new List<IMetadataReference>();
            var sourceReferences = new List<ISourceReference>();

            // Project reference
            metadataReferences.Add(new RoslynProjectReference(compliationContext));

            // Other references
            metadataReferences.AddRange(projectDependenciesExport.MetadataReferences);

            // Shared sources
            foreach (var sharedFile in project.SharedFiles)
            {
                sourceReferences.Add(new SourceFileReference(sharedFile));
            }

            return new LibraryExport(metadataReferences, sourceReferences);
        }
    }
}