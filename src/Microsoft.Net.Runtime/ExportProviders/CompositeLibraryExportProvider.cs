using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime
{
    public class CompositeLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IEnumerable<ILibraryExportProvider> _libraryExporters;

        public CompositeLibraryExportProvider(IEnumerable<ILibraryExportProvider> libraryExporters)
        {
            _libraryExporters = libraryExporters;
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            return _libraryExporters.Select(r => r.GetLibraryExport(name, targetFramework))
                                             .FirstOrDefault(export => export != null);
        }
    }
}
