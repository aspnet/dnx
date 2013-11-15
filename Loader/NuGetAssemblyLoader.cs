using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet;

namespace Loader
{
    public class NuGetAssemblyLoader : IAssemblyLoader, IDependencyResolver
    {
        private readonly LocalPackageRepository _repository;
        private readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public NuGetAssemblyLoader(string packagesDirectory)
        {
            _repository = new LocalPackageRepository(packagesDirectory);
        }

        public Assembly Load(LoadOptions options)
        {
            string name = options.AssemblyName;

            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            string path;
            if (_paths.TryGetValue(name, out path))
            {
                assembly = Assembly.LoadFile(path);

                _cache[name] = assembly;
            }

            return assembly;
        }

        public IEnumerable<Dependency> GetDependencies(string name, SemanticVersion version)
        {
            foreach (var p in FindCandidates(name, version))
            {
                if (version == null)
                {
                    return GetDependencies(p);
                }
                else if (p.Version == version)
                {
                    return GetDependencies(p);
                }
                else
                {
                    // Find matching assemblies in the package and select that package
                    foreach (var fileName in GetAssemblies(p))
                    {
                        var an = AssemblyName.GetAssemblyName(fileName);

                        if (new SemanticVersion(an.Version) == version)
                        {
                            return GetDependencies(p);
                        }
                    }
                }
            }

            return null;
        }

        private IEnumerable<Dependency> GetDependencies(IPackage package)
        {
            var framework = VersionUtility.ParseFrameworkName("net45");
            IEnumerable<PackageDependencySet> dependencySet;
            if (VersionUtility.TryGetCompatibleItems(framework, package.DependencySets, out dependencySet))
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
                            yield return new Dependency
                            {
                                Name = dependency.Id,
                                Version = dependency.Version
                            };
                        }
                    }
                }
            }
        }

        public void Initialize(IEnumerable<Dependency> dependencies)
        {
            foreach (var d in dependencies)
            {
                foreach (var package in FindCandidates(d.Name, d.Version))
                {
                    foreach (var fileName in GetAssemblies(package))
                    {
                        var an = AssemblyName.GetAssemblyName(fileName);

                        _paths[an.FullName] = fileName;
                        _paths[an.Name] = fileName;

                        if (!_paths.ContainsKey(package.Id))
                        {
                            _paths[package.Id] = fileName;
                        }
                    }
                }
            }
        }

        private IEnumerable<string> GetAssemblies(IPackage package)
        {
            var path = _repository.PathResolver.GetInstallPath(package);

            var framework = VersionUtility.ParseFrameworkName("net45");
            IEnumerable<IPackageAssemblyReference> references;
            if (VersionUtility.TryGetCompatibleItems(framework, package.AssemblyReferences, out references))
            {
                foreach (var reference in references)
                {
                    string fileName = Path.Combine(path, reference.Path);
                    if (File.Exists(fileName))
                    {
                        yield return fileName;
                    }
                }
            }
        }

        private IEnumerable<IPackage> FindCandidates(string name, SemanticVersion version)
        {
            if (version == null)
            {
                return _repository.FindPackagesById(name);
            }

            var package = _repository.FindPackage(name, version);

            if (package == null)
            {
                return _repository.FindPackagesById(name);
            }

            return new[] { package };
        }
    }
}
