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

        public NuGetDependencyResolver(string packagesPath, IFrameworkReferenceResolver frameworkReferenceResolver)
        {
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
                Path.Combine(_repository.RepositoryRoot.Root, "{name}", "{version}", "{name}.nuspec")
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
                    if (!assemblyReference.SupportedFrameworks.Any() &&
                        !VersionUtility.IsDesktop(targetFramework))
                    {
                        // REVIEW: This isn't 100% correct since none *can* mean 
                        // any in theory, but in practice it means .NET full reference assembly
                        // If there's no supported target frameworks and we're not targeting
                        // the desktop framework then skip it.

                        // To do this properly we'll need all reference assemblies supported
                        // by each supported target framework which isn't always available.
                        continue;
                    }

                    yield return new Library
                    {
                        Name = assemblyReference.AssemblyName
                    };
                }
            }
        }

        public void Initialize(IEnumerable<LibraryDescription> packages, FrameworkName targetFramework)
        {
            Dependencies = packages;

            var cacheResolvers = GetCacheResolvers();
            var defaultResolver = new DefaultPackagePathResolver(_repository.RepositoryRoot);

            foreach (var dependency in packages)
            {
                var package = FindCandidate(dependency.Identity.Name, dependency.Identity.Version);

                if (package == null)
                {
                    continue;
                }

                string packagePath = ResolvePackagePath(defaultResolver, cacheResolvers, package);

                dependency.Type = "Package";
                dependency.Path = packagePath;

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

                packageDescription.PackageAssemblies = GetAssemblies(packagePath, package, targetFramework);

                foreach (var assemblyInfo in packageDescription.PackageAssemblies)
                {
                    _packageAssemblyPaths[assemblyInfo.Name] = assemblyInfo.Path;
                }

                packageDescription.FrameworkAssemblies = GetFrameworkAssemblies(package, targetFramework);

                var sharedSources = GetSharedSources(packagePath, package, targetFramework).ToList();
                if (!sharedSources.IsEmpty())
                {
                    packageDescription.SharedSources = sharedSources;
                }
            }
        }

        private static string ResolvePackagePath(IPackagePathResolver defaultResolver,
                                                 IEnumerable<IPackagePathResolver> cacheResolvers,
                                                 IPackage package)
        {
            var defaultHashPath = defaultResolver.GetHashPath(package.Id, package.Version);

            foreach (var resolver in cacheResolvers)
            {
                var cacheHashFile = resolver.GetHashPath(package.Id, package.Version);

                // REVIEW: More efficient compare?
                if (File.Exists(defaultHashPath) &&
                    File.Exists(cacheHashFile) &&
                    File.ReadAllText(defaultHashPath) == File.ReadAllText(cacheHashFile))
                {
                    return resolver.GetInstallPath(package.Id, package.Version);
                }
            }

            return defaultResolver.GetInstallPath(package.Id, package.Version);
        }

        private static IEnumerable<IPackagePathResolver> GetCacheResolvers()
        {
            var packageCachePathValue = Environment.GetEnvironmentVariable("KRE_PACKAGES_CACHE");

            if (string.IsNullOrEmpty(packageCachePathValue))
            {
                return Enumerable.Empty<IPackagePathResolver>();
            }

            return packageCachePathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(path => new DefaultPackagePathResolver(path));
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
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

        private List<AssemblyDescription> GetAssemblies(string packagePath, IPackage package, FrameworkName frameworkName)
        {
            return GetAssembliesFromPackage(package, frameworkName, packagePath);
        }

        private IEnumerable<string> GetSharedSources(string packagePath, IPackage package, FrameworkName targetFramework)
        {
            var directory = Path.Combine(packagePath, "shared");
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories);
        }

        private List<AssemblyDescription> GetFrameworkAssemblies(IPackage package, FrameworkName targetFramework)
        {
            var results = new List<AssemblyDescription>();

            IEnumerable<FrameworkAssemblyReference> frameworkAssemblies;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, package.FrameworkAssemblies, out frameworkAssemblies))
            {
                foreach (var reference in frameworkAssemblies)
                {
                    string path;
                    if (_frameworkReferenceResolver.TryGetAssembly(reference.AssemblyName, targetFramework, out path))
                    {
                        results.Add(new AssemblyDescription
                        {
                            Name = reference.AssemblyName,
                            Path = path
                        });
                    }
                }
            }

            return results;
        }

        private static List<AssemblyDescription> GetAssembliesFromPackage(IPackage package, FrameworkName targetFramework, string path)
        {
            var results = new List<AssemblyDescription>();

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
                    results.Add(new AssemblyDescription
                    {
                        // Remove the .dll extension
                        Name = Path.GetFileNameWithoutExtension(reference.Name),
                        Path = fileName
                    });
                }
            }

            return results;
        }

        public IPackage FindCandidate(string name, SemanticVersion version)
        {
            var packages = _repository.FindPackagesById(name);

            if (version == null)
            {
                // TODO: Disallow null versions for nuget packages
                var packageInfo = packages.FirstOrDefault();
                if (packageInfo != null)
                {
                    return packageInfo.Package;
                }

                return null;
            }

            PackageInfo bestMatch = null;

            foreach (var packageInfo in packages)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestMatch != null ? bestMatch.Version : null,
                    considering: packageInfo.Version,
                    ideal: version))
                {
                    bestMatch = packageInfo;
                }
            }

            if (bestMatch == null)
            {
                return null;
            }

            return bestMatch.Package;
        }

        public static string ResolveRepositoryPath(string rootDirectory)
        {
            // Order
            // 1. KRE_PACKAGES environment variable
            // 2. global.json { "packages": "..." }
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. %userprofile%\.kpm\packages

            var krePackages = Environment.GetEnvironmentVariable("KRE_PACKAGES");

            if (!string.IsNullOrEmpty(krePackages))
            {
                return krePackages;
            }

            GlobalSettings settings;
            if (GlobalSettings.TryGetGlobalSettings(rootDirectory, out settings) &&
                !string.IsNullOrEmpty(settings.PackagesPath))
            {
                return Path.Combine(rootDirectory, settings.PackagesPath);
            }

            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            return Path.Combine(profileDirectory, ".kpm", "packages");
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
