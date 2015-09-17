using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public class RuntimeLibraryExporter : ILibraryExporter
    {
        private Project project;
        private string _configuration;
        private CompilationEngine _engine;
        private Project _project;
        private FrameworkName _targetFramework;

        public RuntimeLibraryExporter(CompilationEngine engine, Project project, FrameworkName targetFramework, string configuration) 
        {
            _engine = engine;
            _project = project;
            _targetFramework = targetFramework;
            _configuration = configuration;
        }

        public LibraryExport GetAllExports(string name)
        {
            // _engine.CreateProjectExporter(null).ExportProject
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
