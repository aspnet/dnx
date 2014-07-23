using System;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// Summary description for RoslynLibraryExportProviderExtensions
    /// </summary>
    public static class RoslynLibraryExportProviderExtensions
    {
        public static CompilationContext GetCompilationContext(this ILibraryExportProvider libraryExportProvider, string name, FrameworkName targetFramework, string configuration)
        {
            var export = libraryExportProvider.GetLibraryExport(name, targetFramework, configuration);

            if (export == null)
            {
                return null;
            }

            // This has all transitive project references so we can just cache up front
            foreach (var projectReference in export.MetadataReferences.OfType<RoslynProjectReference>())
            {
                if (string.Equals(projectReference.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectReference.CompilationContext;
                }
            }

            return null;

        }
    }
}