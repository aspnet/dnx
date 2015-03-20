using System;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.PackageManager
{
    public class ProjectBuilder
    {
        private readonly ILibraryManager _libraryManager;

        public ProjectBuilder(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public IDiagnosticResult Build(string name, string outputPath)
        {
            var export = _libraryManager.GetLibraryExport(name);

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