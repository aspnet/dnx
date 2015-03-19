using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Dependencies;
using Microsoft.Framework.Runtime.Internal;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Compilation
{
    public class LibraryExporter
    {
        public static readonly string LibraryExportLibraryPropertyName = "Microsoft.Framework.Runtime.Compilation.LibraryExport";

        private static readonly ILogger Log = RuntimeLogging.Logger<LibraryExporter>();
        private readonly NuGetFramework _targetFramework;
        private readonly PackagePathResolver _packagePathResolver;

        public LibraryExporter(
            NuGetFramework targetFramework, 
            PackagePathResolver packagePathResolver)
        {
            _targetFramework = targetFramework;
            _packagePathResolver = packagePathResolver;
        }

        /// <summary>
        /// Creates a <see cref="ILibraryExport"/> containing the references necessary
        /// to use the provided <see cref="Library"/> during compilation.
        /// </summary>
        /// <param name="library">The <see cref="Library"/> to export</param>
        /// <returns>A <see cref="ILibraryExport"/> containing the references exported by this library</returns>
        public ILibraryExport ExportLibrary(Library library, DependencyManager dependencies)
        {
            // Check if we have a value cached on the Library
            var export = library.GetItem<ILibraryExport>(LibraryExportLibraryPropertyName);
            if (export != null)
            {
                return export;
            }

            switch (library.Identity.Type)
            {
                case LibraryTypes.Package:
                    export = ExportPackageLibrary(library);
                    break;
                case LibraryTypes.Project:
                    export = ExportProjectLibrary(library, dependencies);
                    break;
                default:
                    export = ExportOtherLibrary(library);
                    break;
            }

            // Cache for later use.
            library[LibraryExportLibraryPropertyName] = export;
            LogExport(library, export);
            return export;
        }

        private ILibraryExport ExportOtherLibrary(Library library)
        {
            // Try to create an export for a library of other or unknown type
            // based on well-known properties.

            // Reference Assemblies just put the full path in a property for us.
            var path = library.GetItem<string>(KnownLibraryProperties.AssemblyPath);
            if (!string.IsNullOrEmpty(path))
            {
                return new LibraryExport(
                    new MetadataFileReference(
                        Path.GetFileNameWithoutExtension(path),
                        path));
            }

            Log.LogWarning($"Unable to export {library.Identity}. {library.Identity.Type} libraries are not supported.");
            return LibraryExport.Empty;
        }

        private ILibraryExport ExportPackageLibrary(Library library)
        {
            // Get the lock file group and library
            var group = library.GetRequiredItem<LockFileFrameworkGroup>(KnownLibraryProperties.LockFileFrameworkGroup);
            var lockFileLibrary = library.GetRequiredItem<LockFileLibrary>(KnownLibraryProperties.LockFileLibrary);

            // Resolve the package root
            var packageRoot = _packagePathResolver.ResolvePackagePath(
                lockFileLibrary.Sha,
                lockFileLibrary.Name,
                lockFileLibrary.Version);

            // Grab the compile time assemblies and their full paths
            var metadataReferences = new List<IMetadataReference>();
            foreach (var compileTimeAssembly in group.CompileTimeAssemblies)
            {
                var reference = new MetadataFileReference(
                    Path.GetFileNameWithoutExtension(compileTimeAssembly),
                    Path.Combine(packageRoot, compileTimeAssembly));

                metadataReferences.Add(reference);
            }

            return new LibraryExport(metadataReferences);
        }

        private ILibraryExport ExportProjectLibrary(Library library, DependencyManager dependencies)
        {
            // Yes, this code is useless right now, but it dumps to the console
            // so I'm keeping it for now :)

            // Get dependencies and export them
            var exports = dependencies.EnumerateAllDependencies(library)
                .Select(lib => ExportLibrary(lib, dependencies))
                .ToList();
            return LibraryExport.Empty;
        }

        private void LogExport(Library library, ILibraryExport export)
        {
            if(Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug($"    Exporting {library.Identity}");
                foreach(var reference in Enumerable.Concat<object>(export.MetadataReferences, export.SourceReferences).Where(o => o != null))
                {
                    Log.LogDebug($"      {reference}");
                }
            }
        }
    }
}