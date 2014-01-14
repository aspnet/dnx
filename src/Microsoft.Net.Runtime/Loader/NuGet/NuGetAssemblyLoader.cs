using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using NuGet;

namespace Microsoft.Net.Runtime.Loader.NuGet
{
    public class NuGetAssemblyLoader : IPackageLoader, IMetadataLoader
    {
        private readonly LocalPackageRepository _repository;
        private readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public NuGetAssemblyLoader(string projectPath)
        {
            _repository = new LocalPackageRepository(ResolveRepositoryPath(projectPath));
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string name = loadContext.AssemblyName;

            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return new AssemblyLoadResult(assembly);
            }

            string path;
            if (_paths.TryGetValue(name, out path))
            {
                assembly = Assembly.LoadFile(path);

                _cache[name] = assembly;
            }

            if (assembly == null)
            {
                return null;
            }

            return new AssemblyLoadResult(assembly);
        }

        public PackageDetails GetDetails(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            var package = FindCandidate(name, version);

            if (package != null)
            {
                return new PackageDetails
                {
                    Identity = new PackageReference { Name = package.Id, Version = package.Version },
                    Dependencies = GetDependencies(package, frameworkName)
                };
            }

            return null;
        }

        private IEnumerable<PackageReference> GetDependencies(IPackage package, FrameworkName frameworkName)
        {
            IEnumerable<PackageDependencySet> dependencySet;
            if (VersionUtility.TryGetCompatibleItems(frameworkName, package.DependencySets, out dependencySet))
            {
                foreach (var set in dependencySet)
                {
                    foreach (var d in set.Dependencies)
                    {
                        var dependency = _repository.FindPackagesById(d.Id)
                                                    .Where(d.VersionSpec.ToDelegate())
                                                    .FirstOrDefault();
                        if (dependency != null)
                        {
                            yield return new PackageReference
                            {
                                Name = dependency.Id,
                                Version = dependency.Version
                            };
                        }
                    }
                }
            }
        }

        public void Initialize(IEnumerable<PackageReference> packages, FrameworkName frameworkName)
        {
            foreach (var dependency in packages)
            {
                var package = FindCandidate(dependency.Name, dependency.Version);

                if (package == null)
                {
                    continue;
                }

                foreach (var fileName in GetAssemblies(package, frameworkName))
                {
#if DESKTOP // CORECLR_TODO: AssemblyName.GetAssemblyName
                    var an = AssemblyName.GetAssemblyName(fileName);
                    _paths[an.Name] = fileName;
#else
                    _paths[Path.GetFileNameWithoutExtension(fileName)] = fileName;                
#endif

                    if (!_paths.ContainsKey(package.Id))
                    {
                        _paths[package.Id] = fileName;
                    }
                }
            }
        }

        private IEnumerable<string> GetAssemblies(IPackage package, FrameworkName frameworkName)
        {
            var path = _repository.PathResolver.GetInstallPath(package);

            var directory = Path.Combine(path, "lib", VersionUtility.GetShortFrameworkName(frameworkName));

            if (!System.IO.Directory.Exists(directory))
            {
                return GetAssembliesFromPackage(package, frameworkName, path);
            }

            return System.IO.Directory.EnumerateFiles(directory, "*.dll");
        }

        public MetadataReference GetMetadata(string name)
        {
            string path;
            if (_paths.TryGetValue(name, out path))
            {
                return new MetadataFileReference(path);
            }

            return null;
        }

        private static IEnumerable<string> GetAssembliesFromPackage(IPackage package, FrameworkName frameworkName, string path)
        {
            IEnumerable<IPackageAssemblyReference> references;
            if (VersionUtility.TryGetCompatibleItems(frameworkName, package.AssemblyReferences, out references))
            {
                foreach (var reference in references)
                {
                    string fileName = Path.Combine(path, reference.Path);
                    yield return fileName;
                }
            }
        }

        private IPackage FindCandidate(string name, SemanticVersion version)
        {
            if (version == null)
            {
                return _repository.FindPackagesById(name).FirstOrDefault();
            }

            return _repository.FindPackage(name, version);
        }

        private string ResolveRepositoryPath(string projectPath)
        {
            var di = new DirectoryInfo(projectPath);

            string rootPath = null;

            while (di.Parent != null)
            {
                if (di.EnumerateDirectories("packages").Any() ||
                    di.EnumerateFiles("*.sln").Any())
                {
                    rootPath = di.FullName;
                    break;
                }

                di = di.Parent;
            }

            rootPath = rootPath ?? Path.GetDirectoryName(projectPath);

            return Path.Combine(rootPath, "packages");
        }
    }
}
