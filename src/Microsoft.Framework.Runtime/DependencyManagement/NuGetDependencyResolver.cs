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
        private readonly PackageRepository _repository;

        // Assembly name and path lifted from the appropriate lib folder
        private readonly Dictionary<string, string> _packageAssemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // All the information required by this package
        private readonly Dictionary<string, PackageDescription> _packageDescriptions = new Dictionary<string, PackageDescription>(StringComparer.OrdinalIgnoreCase);

        private readonly IFrameworkReferenceResolver _frameworkReferenceResolver;

        public NuGetDependencyResolver(string projectPath, IFrameworkReferenceResolver frameworkReferenceResolver)
            : this(projectPath, null, frameworkReferenceResolver)
        {
        }

        public NuGetDependencyResolver(string projectPath, string packagesPath, IFrameworkReferenceResolver frameworkReferenceResolver)
        {
            if (string.IsNullOrEmpty(packagesPath))
            {
                packagesPath = ResolveRepositoryPath(projectPath);
            }

            _repository = new PackageRepository(packagesPath);
            _frameworkReferenceResolver = frameworkReferenceResolver;
            Dependencies = Enumerable.Empty<LibraryDescription>();
        }

        public IDictionary<string, string> PackageAssemblyPaths
        {
            get
            {
                return _packageAssemblyPaths;
            }
        }

        public IEnumerable<LibraryDescription> Dependencies { get; private set; }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return new[]
            {
                Path.Combine(_repository.FileSystem.Root, "{name}.{version}", "{name}.nuspec"),
                Path.Combine(_repository.FileSystem.Root, "{name}.{version}", "{name}.{version}.nupkg")
            };
        }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            var package = FindCandidate(name, version);

            if (package != null)
            {
                return new LibraryDescription
                {
                    Identity = new Library { Name = package.Id, Version = package.Version },
                    Dependencies = GetDependencies(package, targetFramework)
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
                        yield return new Library
                        {
                            Name = d.Id,
                            Version = d.VersionSpec != null ? d.VersionSpec.MinVersion : null
                        };
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
                var package = FindCandidate(dependency.Identity.Name, dependency.Identity.Version);

                if (package == null)
                {
                    continue;
                }

                dependency.Type = "Package";
                dependency.Path = _repository.PathResolver.GetInstallPath(package);

                var packageDescription = new PackageDescription
                {
                    Library = dependency
                };

                _packageDescriptions[package.Id] = packageDescription;

                // Try to find a contract folder for this package and store that
                // for compilation
                string contractPath = Path.Combine(dependency.Path, "lib", "contract",
                                                   package.Id + ".dll");
                if (File.Exists(contractPath))
                {
                    packageDescription.ContractPath = contractPath;
                }

                var assemblies = GetAssemblies(package, targetFramework).ToList();
                foreach (var path in assemblies)
                {
                    var assemblyName = GetAssemblyName(path);
                    packageDescription.PackageAssemblies.Add(new AssemblyDescription
                    {
                        Name = assemblyName.Name,
                        Path = path
                    });

                    _packageAssemblyPaths[assemblyName.Name] = path;
                }

                var frameworkReferences = GetFrameworkAssemblies(package, targetFramework).ToList();
                if (!frameworkReferences.IsEmpty())
                {
                    foreach (var path in frameworkReferences)
                    {
                        var assemblyName = GetAssemblyName(path);
                        packageDescription.FrameworkAssemblies.Add(new AssemblyDescription
                        {
                            Name = assemblyName.Name,
                            Path = path
                        });
                    }
                }

                var sharedSources = GetSharedSources(package, targetFramework).ToList();
                if (!sharedSources.IsEmpty())
                {
                    packageDescription.SharedSources = sharedSources;
                }
            }
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            if (!_packageDescriptions.ContainsKey(name))
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

            // REVIEW: This requires more design
            var sourceReferences = new List<ISourceReference>();
            PackageDescription description;
            if (_packageDescriptions.TryGetValue(name, out description))
            {
                foreach (var sharedSource in description.SharedSources)
                {
                    sourceReferences.Add(new SourceFileReference(sharedSource));
                }
            }

            return new LibraryExport(metadataReferenes, sourceReferences);
        }

        private void PopulateDependenciesPaths(string name, IDictionary<string, string> paths)
        {
            PackageDescription description;
            if (!_packageDescriptions.TryGetValue(name, out description))
            {
                paths[name] = null;
                return;
            }

            // Use contract if both contract and target path are available
            bool hasContract = description.ContractPath != null;
            bool hasLib = description.PackageAssemblies.Any();

            if (hasContract && hasLib)
            {
                paths[name] = description.ContractPath;
            }
            else if (hasLib)
            {
                foreach (var assembly in description.PackageAssemblies)
                {
                    paths[assembly.Name] = assembly.Path;
                }
            }

            foreach (var dependency in description.Library.Dependencies)
            {
                PopulateDependenciesPaths(dependency.Name, paths);
            }

            // Overwrite paths that may not have been found with framework
            // references
            foreach (var assembly in description.FrameworkAssemblies)
            {
                paths[assembly.Name] = assembly.Path;
            }
        }

        private IEnumerable<string> GetAssemblies(IPackage package, FrameworkName frameworkName)
        {
            var path = _repository.PathResolver.GetInstallPath(package);

            return GetAssembliesFromPackage(package, frameworkName, path);
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

        private static IEnumerable<string> GetAssembliesFromPackage(IPackage package, FrameworkName targetFramework, string path)
        {
            IEnumerable<IPackageAssemblyReference> compatibleReferences;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, package.AssemblyReferences, out compatibleReferences))
            {
                // Get the list of references for this target framework
                var references = compatibleReferences.ToList();

                // See if there's a list of specific references defined for this target framework
                IEnumerable<PackageReferenceSet> referenceSets;
                if (VersionUtility.TryGetCompatibleItems(targetFramework, package.PackageAssemblyReferences, out referenceSets))
                {
                    // Get the first compatible reference set
                    var referenceSet = referenceSets.FirstOrDefault();

                    if (referenceSet != null)
                    {
                        // Remove all assemblies of which names do not appear in the References list
                        references.RemoveAll(r => !referenceSet.References.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
                    }
                }

                foreach (var reference in references)
                {
                    // Skip anything that isn't a dll. Unfortunately some packages put random stuff
                    // in the lib folder and they surface as assembly references
                    if (!Path.GetExtension(reference.Path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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

            var packages = _repository.FindPackagesById(name);
            IPackage bestMatch = null;

            foreach (var package in packages)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestMatch != null ? bestMatch.Version : null,
                    considering: package.Version,
                    ideal: version))
                {
                    bestMatch = package;
                }
            }

            return bestMatch;
        }

        public static string ResolveRepositoryPath(string projectPath)
        {
            var rootPath = ProjectResolver.ResolveRootDirectory(projectPath);

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

        private class PackageDescription
        {
            public LibraryDescription Library { get; set; }

            public string ContractPath { get; set; }

            public List<AssemblyDescription> PackageAssemblies { get; set; }

            public List<AssemblyDescription> FrameworkAssemblies { get; set; }

            public List<string> SharedSources { get; set; }

            public PackageDescription()
            {
                PackageAssemblies = new List<AssemblyDescription>();
                FrameworkAssemblies = new List<AssemblyDescription>();
                SharedSources = new List<string>();
            }
        }

        private class AssemblyDescription
        {
            public string Name { get; set; }

            public string Path { get; set; }
        }
    }
}
