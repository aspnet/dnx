using System;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public class RuntimeLibraryExporter : ILibraryExporter
    {
        private readonly CompilationEngine _compilationEngine;

        public RuntimeLibraryExporter(CompilationEngine compilationEngine)
        {
            _compilationEngine = compilationEngine;
        }

        public LibraryExport GetAllExports(string name)
        {
            // return _exporter.Value.GetAllExports(name);
            return null;
        }

        public LibraryExport GetExport(string name)
        {
            // return _exporter.Value.GetExport(name);
            return null;
        }
    }
}
