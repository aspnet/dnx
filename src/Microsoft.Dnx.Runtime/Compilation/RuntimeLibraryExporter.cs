using System;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public class RuntimeLibraryExporter : ILibraryExporter
    {
        private Lazy<ILibraryExporter> _exporter;

        public RuntimeLibraryExporter(Func<ILibraryExporter> exporterFactory)
        {
            _exporter = new Lazy<ILibraryExporter>(exporterFactory);
        }

        public LibraryExport GetAllExports(string name)
        {
            return _exporter.Value.GetAllExports(name);
        }

        public LibraryExport GetExport(string name)
        {
            return _exporter.Value.GetExport(name);
        }
    }
}
