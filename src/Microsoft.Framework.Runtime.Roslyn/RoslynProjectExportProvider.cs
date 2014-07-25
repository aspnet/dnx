using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// Summary description for RoslynLibraryExportProvider
    /// </summary>
    public class RoslynProjectExportProvider : IProjectExportProvider
    {
        private readonly RoslynCompiler _compiler;

        public RoslynProjectExportProvider(IFileWatcher watcher)
        {
            _compiler = new RoslynCompiler(watcher);
        }

        public ILibraryExport GetProjectExport(
            Project project,
            FrameworkName targetFramework,
            string configuration,
            ILibraryExport projectDependenciesExport)
        {
            var compliationContext = _compiler.CompileProject(project, targetFramework, configuration, projectDependenciesExport);

            if (compliationContext == null)
            {
                return null;
            }

            return compliationContext.GetLibraryExport();
        }
    }
}