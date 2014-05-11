// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class NuGetDependencyResolver : IDependencyProvider, ILibraryExportProvider
    {
        private readonly LocalPackageRepository _repository;
        private readonly Dictionary<string, string> _contractPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _frameworkAssemblies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LibraryDescription> _dependencies = new Dictionary<string, LibraryDescription>(StringComparer.OrdinalIgnoreCase);
        private readonly IDictionary<string, IList<string>> _sharedSources = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly FrameworkReferenceResolver _frameworkReferenceResolver;

        public NuGetDependencyResolver(string projectPath, FrameworkReferenceResolver frameworkReferenceResolver)
            : this(projectPath, null, frameworkReferenceResolver)
        {
        }

        public NuGetDependencyResolver(string projectPath, string packagesPath, FrameworkReferenceResolver frameworkReferenceResolver)
        {
            if (string.IsNullOrEmpty(packagesPath))
            {
                packagesPath = ResolveRepositoryPath(projectPath);
            }
            _repository = new LocalPackageRepository(packagesPath);
            _frameworkReferenceResolver = frameworkReferenceResolver;
            Dependencies = Enumerable.Empty<LibraryDescription>();
        }

        public IDictionary<string, string> PackageAssemblyPaths
        {
            get
            {
                return _paths;
            }
        }

        public IEnumerable<LibraryDescription> Dependencies { get; private set; }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            var package = FindCandidate(name, version);

            if (package != null)
            {
                return new LibraryDescription
                {
                    Identity = new Library { Name = package.Id, Version = package.Version },
                    Dependencies = GetDependencies(package, frameworkName)
                };
            }

            return null;
        }

        private IEnumerable<Library> GetDependencies(IPackage package, FrameworkName targetFramework)
        {
            IEnumerable<PackageDependencySet> dependencySet;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, package.DependencySets, out dependencySet))
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
                            yield return new Library
                            {
                                Name = dependency.Id,
                                Version = dependency.Version
                            };
                        }
                        else
                        {
                            // REVIEW: What happens to the range
                            yield return new Library
                            {
                                Name = d.Id,
                                Version = d.VersionSpec.MinVersion
                            };
                        }
                    }
                }
            }

            IEnumerable<FrameworkAssemblyReference> frameworkAssemblies;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, package.FrameworkAssemblies, out frameworkAssemblies))
            {
                foreach (var assemblyReference in frameworkAssemblies)
                {
                    string path;
                    if (_frameworkReferenceResolver.TryGetAssembly(assemblyReference.AssemblyName, targetFramework, out path))
                    {
                        yield return new Library
                        {
                            Name = assemblyReference.AssemblyName,
                            Version = VersionUtility.GetAssemblyVersion(path)
                        };
                    }
                }
            }
        }

        public void Initialize(IEnumerable<LibraryDescription> packages, FrameworkName targetFramework)
        {
            Dependencies = packages;

            foreach (var dependency in packages)
            {
                _dependencies[dependency.Identity.Name] = dependency;

                var package = FindCandidate(dependency.Identity.Name, dependency.Identity.Version);

                if (package == null)
                {
                    continue;
                }

                dependency.Type = "Package";
                dependency.Path = _repository.PathResolver.GetInstallPath(package);

                // Try to find a contract folder for this package and store that
                // for compilation
                string contractPath = Path.Combine(_repository.PathResolver.GetInstallPath(package),
                                                   "lib",
                                                   "contract",
                                                   package.Id + ".dll");
                if (File.Exists(contractPath))
                {
                    _contractPaths[package.Id] = contractPath;
                }

                foreach (var fileName in GetAssemblies(package, targetFramework))
                {
                    AssemblyName an = GetAssemblyName(fileName);

                    _paths[an.Name] = fileName;

                    if (!_paths.ContainsKey(package.Id))
                    {
                        _paths[package.Id] = fileName;
                    }
                }

                var frameworkReferences = GetFrameworkAssemblies(package, targetFramework).ToList();
                if (!frameworkReferences.IsEmpty())
                {
                    _frameworkAssemblies[package.Id] = frameworkReferences;
                }

                var sharedSources = GetSharedSources(package, targetFramework).ToList();
                if (!sharedSources.IsEmpty())
                {
                    _sharedSources[dependency.Identity.Name] = sharedSources;
                }
            }
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            if (!_dependencies.ContainsKey(name))
            {
                return null;
            }

            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            PopulateDependenciesPaths(name, paths);

            var metadataReferenes = paths.Select(pair =>
            {
                IMetadataReference reference = null;

                if (pair.Value == null)
                {
                    reference = new UnresolvedMetadataReference(pair.Key);
                }
                else
                {
                    reference = new MetadataFileReference(pair.Key, pair.Value);
                }

                return reference;
            }).ToList();

            var sourceReferences = new List<ISourceReference>();

            IList<string> sharedSources;
            if (_sharedSources.TryGetValue(name, out sharedSources))
            {
                foreach (var sharedSource in sharedSources)
                {
                    sourceReferences.Add(new SourceFileReference(sharedSource));
                }
            }

            return new LibraryExport(metadataReferenes, sourceReferences);
        }

        private void PopulateDependenciesPaths(string name, IDictionary<string, string> paths)
        {
            LibraryDescription description;
            if (!_dependencies.TryGetValue(name, out description))
            {
                paths[name] = null;
                return;
            }

            // Use contract if both contract and target path are available
            string contractPath;
            bool hasContract = _contractPaths.TryGetValue(name, out contractPath);

            string libPath;
            bool hasLib = _paths.TryGetValue(name, out libPath);

            if (hasContract && hasLib)
            {
                paths[name] = contractPath;
            }
            else if (hasLib)
            {
                paths[name] = libPath;
            }

            foreach (var dependency in description.Dependencies)
            {
                PopulateDependenciesPaths(dependency.Name, paths);
            }

            // Overwrite paths that may not have been found with framework
            // references
            List<string> frameworkAssemblies;
            if (_frameworkAssemblies.TryGetValue(name, out frameworkAssemblies))
            {
                foreach (var assemblyPath in frameworkAssemblies)
                {
                    var an = GetAssemblyName(assemblyPath);
                    paths[an.Name] = assemblyPath;
                }
            }
        }

        private IEnumerable<string> GetAssemblies(IPackage package, FrameworkName frameworkName)
        {
            var path = _repository.PathResolver.GetInstallPath(package);

            var directory = Path.Combine(path, "lib", VersionUtility.GetShortFrameworkName(frameworkName));

            if (!Directory.Exists(directory))
            {
                return GetAssembliesFromPackage(package, frameworkName, path);
            }

            return Directory.EnumerateFiles(directory, "*.dll");
        }

        private IEnumerable<string> GetSharedSources(IPackage package, FrameworkName targetFramework)
        {
            var path = _repository.PathResolver.GetInstallPath(package);

            var directory = Path.Combine(path, "shared");
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories);
        }

        private IEnumerable<string> GetFrameworkAssemblies(IPackage package, FrameworkName targetFramework)
        {
            IEnumerable<FrameworkAssemblyReference> frameworkAssemblies;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, package.FrameworkAssemblies, out frameworkAssemblies))
            {
                foreach (var reference in frameworkAssemblies)
                {
                    string path;
                    if (_frameworkReferenceResolver.TryGetAssembly(reference.AssemblyName, targetFramework, out path))
                    {
                        yield return path;
                    }
                }
            }
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

        public IPackage FindCandidate(string name, SemanticVersion version)
        {
            if (version == null)
            {
                return _repository.FindPackagesById(name).FirstOrDefault();
            }
            else if (version.IsSnapshot)
            {
                return _repository.FindPackagesById(name)
                    .OrderByDescending(pk => pk.Version.SpecialVersion, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(pk => pk.Version.EqualsSnapshot(version));
            }
            return _repository.FindPackage(name, version);
        }

        public static string ResolveRepositoryPath(string projectPath)
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

        private static AssemblyName GetAssemblyName(string fileName)
        {
#if NET45
            return AssemblyName.GetAssemblyName(fileName);
#else
            return System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(fileName);
#endif
        }

    }
}
