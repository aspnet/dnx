using System;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    public class ProjectBuilder
    {
        private readonly ILibraryExportProvider _libraryExportProvider;

        public ProjectBuilder(ILibraryExportProvider libraryExportProvider)
        {
            _libraryExportProvider = libraryExportProvider;
        }

        public IProjectBuildResult Build(string name,
                                         FrameworkName targetFramework,
                                         string configuration,
                                         string outputPath)
        {
            var export = _libraryExportProvider.GetLibraryExport(name, targetFramework, configuration);

            if (export == null)
            {
                return null;
            }

            foreach (var projectReference in export.MetadataReferences.OfType<IMetadataProjectReference>())
            {
                if (string.Equals(projectReference.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectReference.EmitAssembly(outputPath);
                }
            }

            return null;
        }
    }
}