using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class ReferenceAssemblyDependencyResolver : IDependencyProvider, ILibraryExportProvider
    {
        private readonly Dictionary<string, string> _resolvedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ReferenceAssemblyDependencyResolver()
        {
            FrameworkResolver = new FrameworkReferenceResolver();
        }

        public FrameworkReferenceResolver FrameworkResolver { get; private set; }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            string path;
            if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path))
            {
                return null;
            }

            SemanticVersion assemblyVersion = VersionUtility.GetAssemblyVersion(path);

            if (version == null || version == assemblyVersion)
            {
                _resolvedPaths[name] = path;

                return new LibraryDescription
                {
                    Identity = new Library { Name = name, Version = assemblyVersion },
                    Dependencies = Enumerable.Empty<Library>()
                };
            }

            return null;
        }

        public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework)
        {
            foreach (var d in dependencies)
            {
                d.Path = _resolvedPaths[d.Identity.Name];
                d.Type = "Assembly";
            }
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            string path;
            if (_resolvedPaths.TryGetValue(name, out path))
            {
                return new LibraryExport(name, path);
            }

            return null;
        }
    }
}
