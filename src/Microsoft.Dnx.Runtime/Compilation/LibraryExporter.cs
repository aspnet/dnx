using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryExporter : ILibraryExporter
    {
        private readonly ILibraryManager _libraryManager;
        private readonly FrameworkName _targetFramework;
        private readonly string _configuration;
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly ICache _cache;

        public LibraryExporter(FrameworkName targetFramework,
                               string configuration,
                               ILibraryManager libraryManager,
                               ILibraryExportProvider libraryExportProvider,
                               ICache cache)
        {
            _targetFramework = targetFramework;
            _configuration = configuration;
            _libraryExportProvider = libraryExportProvider;
            _libraryManager = libraryManager;
            _cache = cache;
        }

        public LibraryExport GetLibraryExport(string name)
        {
            return GetLibraryExport(name, aspect: null);
        }

        public LibraryExport GetAllExports(string name)
        {
            return GetAllExports(name, aspect: null);
        }

        public LibraryExport GetLibraryExport(string name, string aspect)
        {
            return _libraryExportProvider.GetLibraryExport(new CompilationTarget(name, _targetFramework, _configuration, aspect));
        }

        public LibraryExport GetAllExports(string name, string aspect)
        {
            var key = Tuple.Create(
                nameof(LibraryExporter),
                nameof(GetAllExports),
                name,
                _targetFramework,
                _configuration,
                aspect);

            return _cache.Get<LibraryExport>(key, ctx =>
                ProjectExportProviderHelper.GetExportsRecursive(
                    _libraryManager,
                    _libraryExportProvider,
                    new CompilationTarget(name, _targetFramework, _configuration, aspect),
                    dependenciesOnly: false));
        }
    }
}
