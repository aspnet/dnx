using System;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.Runtime
{
    public class ProjectMetadataProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly ILibraryExportProvider _libraryExportProvider;

        public ProjectMetadataProvider(IProjectResolver projectResolver, ILibraryExportProvider libraryExportProvider)
        {
            _projectResolver = projectResolver;
            _libraryExportProvider = libraryExportProvider;
        }

        public ProjectMetadata GetProjectMetadata(string name, FrameworkName targetFramework, string configuration)
        {
            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            var export = _libraryExportProvider.GetLibraryExport(name, targetFramework, configuration);

            if (export == null)
            {
                return null;
            }

            return new ProjectMetadata(project, export);
        }
    }
}