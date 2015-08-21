using System;
using Microsoft.Dnx.Compilation;

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

        public LibraryExport GetAllExports(string name, string aspect)
        {
            return _exporter.Value.GetAllExports(name, aspect);
        }

        public LibraryExport GetExport(string name)
        {
            return _exporter.Value.GetExport(name);
        }

        public LibraryExport GetExport(string name, string aspect)
        {
            return _exporter.Value.GetExport(name, aspect);
        }
    }
}
