using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime.Loader
{
    public class CompositeDependencyExporter : IDependencyExporter
    {
        private readonly IEnumerable<IDependencyExporter> _dependencyExportResolvers;

        public CompositeDependencyExporter(IEnumerable<IDependencyExporter> dependencyExportResolvers)
        {
            _dependencyExportResolvers = dependencyExportResolvers;
        }

        public IDependencyExport GetDependencyExport(string name, FrameworkName targetFramework)
        {
            return _dependencyExportResolvers.Select(r => r.GetDependencyExport(name, targetFramework))
                                             .FirstOrDefault(export => export != null);
        }
    }
}
