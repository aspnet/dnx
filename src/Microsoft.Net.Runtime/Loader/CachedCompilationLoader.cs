using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public class CachedCompilationLoader : IAssemblyLoader, IDependencyProvider, ILibraryExportProvider
    {
        private readonly IProjectResolver _resolver;
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>();
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public CachedCompilationLoader(IAssemblyLoaderEngine loaderEngine, IProjectResolver resolver)
        {
            _resolver = resolver;
            _loaderEngine = loaderEngine;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string name = loadContext.AssemblyName;

            string path;
            if (_paths.TryGetValue(name, out path))
            {
                return new AssemblyLoadResult(_loaderEngine.LoadFile(path));
            }

            return null;
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            string path;
            if (_paths.TryGetValue(name, out path))
            {
                return new LibraryExport(path);
            }

            return null;
        }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            string cachedFile;
            Project project;

            if (!TryGetCachedFileName(name, targetFramework, out cachedFile, out project))
            {
                return null;
            }

            if (File.Exists(cachedFile) &&
                (version == project.Version || version == null))
            {
                var config = project.GetTargetFrameworkConfiguration(targetFramework);

                return new LibraryDescription
                {
                    Identity = new Library { Name = project.Name, Version = project.Version },
                    Dependencies = project.Dependencies.Concat(config.Dependencies),
                };
            }

            return null;
        }

        public void Initialize(IEnumerable<LibraryDescription> packages, FrameworkName targetFramework)
        {
            foreach (var package in packages)
            {
                string cachedFile;
                TryGetCachedFileName(package.Identity.Name, targetFramework, out cachedFile);

                if (File.Exists(cachedFile))
                {
                    _paths[package.Identity.Name] = cachedFile;
                }
            }
        }

        private bool TryGetCachedFileName(string name, FrameworkName targetFramework, out string cachedFile)
        {
            Project project;
            return TryGetCachedFileName(name, targetFramework, out cachedFile, out project);
        }

        private bool TryGetCachedFileName(string name, FrameworkName targetFramework, out string cachedFile, out Project project)
        {
            cachedFile = null;
            if (!_resolver.TryResolveProject(name, out project))
            {
                return false;
            }

            var path = project.ProjectDirectory;
            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(targetFramework);

            cachedFile = Path.Combine(path, "bin", targetFrameworkFolder, name + ".dll");
            return true;
        }
    }
}
