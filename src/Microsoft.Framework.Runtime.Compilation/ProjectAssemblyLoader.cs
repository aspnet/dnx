using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Dependencies;
using Microsoft.Framework.Runtime.Internal;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Compilation
{
    public class ProjectAssemblyLoaderFactory : IAssemblyLoaderFactory
    {
        private readonly LibraryExporter _exporter;

        public ProjectAssemblyLoaderFactory(LibraryExporter exporter)
        {
            _exporter = exporter;
        }

        public IAssemblyLoader Create(NuGetFramework runtimeFramework, IAssemblyLoadContextAccessor loadContextAccessor, DependencyManager dependencies)
        {
            return new ProjectAssemblyLoader(dependencies, _exporter, loadContextAccessor);
        }
    }

    /// <summary>
    /// Loads assemblies by compiling projects
    /// </summary>
    public class ProjectAssemblyLoader : IAssemblyLoader
    {
        private static readonly ILogger Log = RuntimeLogging.Logger<ProjectAssemblyLoader>();

        private readonly LibraryExporter _exporter;
        private readonly DependencyManager _dependencies;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public ProjectAssemblyLoader(DependencyManager dependencies, LibraryExporter exporter, IAssemblyLoadContextAccessor loadContextAccessor)
        {
            _dependencies = dependencies;
            _exporter = exporter;
            _loadContextAccessor = loadContextAccessor;
        }

        public Assembly Load(string name)
        {
            return Load(new AssemblyName(name), _loadContextAccessor.Default);
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, _loadContextAccessor.Default);
        }

        private Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            if (!string.IsNullOrEmpty(assemblyName.CultureName))
            {
                // LOUDO: create culture assemblies from source project
                return null;
            }

            Library library;
            if(!_dependencies.TryGetLibrary(assemblyName.Name, out library) || !string.Equals(library.Identity.Type, LibraryTypes.Project, StringComparison.Ordinal))
            {
                return null;
            }

            // Get the project's export
            var projectExport = _exporter.ExportLibrary(library, _dependencies);

            foreach(var metadataReference in projectExport.MetadataReferences.OfType<IMetadataProjectReference>())
            {
                if (string.Equals(metadataReference.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogDebug($" Loading {metadataReference.Name}");
                    return metadataReference.Load(assemblyName, loadContext);
                }
            }
            Log.LogWarning($"Project {assemblyName} did not produce a loadable output.");
            return null;
        }
    }
}