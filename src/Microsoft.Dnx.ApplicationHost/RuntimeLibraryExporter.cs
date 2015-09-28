using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public class RuntimeLibraryExporter : ILibraryExporter
    {
        private CompilationEngine _compilationEngine;
        private string _configuration;
        private Project _project;
        private FrameworkName _targetFramework;

        public RuntimeLibraryExporter(CompilationEngine compilationEngine, Project project, FrameworkName targetFramework, string configuration)
        {
            _compilationEngine = compilationEngine;
            _project = project;
            _targetFramework = targetFramework;
            _configuration = configuration;
        }

        public LibraryExport GetAllExports(string name)
        {
            // fix this
            return null;
        }

        public LibraryExport GetExport(string name)
        {
            // fix this
            return null;
        }
    }
}
