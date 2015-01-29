// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class NuGetDependencyResolver : IDependencyProvider, ILibraryExportProvider
    {
        private readonly PackageRepository _repository;

        // Assembly name and path lifted from the appropriate lib folder
        private readonly Dictionary<string, PackageAssembly> _packageAssemblyLookup = new Dictionary<string, PackageAssembly>(StringComparer.OrdinalIgnoreCase);

        // All the information required by this package
        private readonly Dictionary<string, PackageDescription> _packageDescriptions = new Dictionary<string, PackageDescription>(StringComparer.OrdinalIgnoreCase);

        private readonly GlobalSettings _globalSettings;

        public NuGetDependencyResolver(string packagesPath, string rootDir = null)
        {
            // Runtime already ensures case-sensitivity, so we don't need package ids in accurate casing here
            _repository = new PackageRepository(packagesPath, caseSensitivePackagesName: false);
            Dependencies = Enumerable.Empty<LibraryDescription>();

            if (!string.IsNullOrEmpty(rootDir))
            {
                GlobalSettings.TryGetGlobalSettings(rootDir, out _globalSettings);
            }
        }

        public IDictionary<string, PackageAssembly> PackageAssemblyLookup
        {
            get
            {
                return _packageAssemblyLookup;
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

        public LibraryDescription GetDescription(LibraryRange libraryRange, FrameworkName targetFramework)
        {
            if (libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            var package = FindCandidate(libraryRange.Name, libraryRange.VersionRange);

            if (package != null)
            {
                return new LibraryDescription
                {
                    LibraryRange = libraryRange,
                    Identity = new Library
                    {
                        Name = package.Id,
                        Version = package.Version
                    },
                    Type = "Package",
                    Dependencies = GetDependencies(package, targetFramework)
                };
            }

            return null;
        }

        private IEnumerable<LibraryDependency> GetDependencies(IPackage package, FrameworkName targetFramework)
        {
            IEnumerable<PackageDependencySet> dependencySet;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, package.DependencySets, out dependencySet))
            {
                foreach (var set in dependencySet)
                {
                    foreach (var d in set.Dependencies)
                    {
                        yield return new LibraryDependency
                        {
                            LibraryRange = new LibraryRange
                            {
                                Name = d.Id,
                                VersionRange = d.VersionSpec == null ? null : new SemanticVersionRange(d.VersionSpec)
                            }
                        };
                    }
                }
            }

            // TODO: Remove this when we do #596
            // ASP.NET Core isn't compatible with generic PCL profiles
            if (string.Equals(targetFramework.Identifier, VersionUtility.AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
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

                    yield return new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = assemblyReference.AssemblyName,
                            IsGacOrFrameworkReference = true
                        }
                    };
                }
            }
        }

        public void Initialize(IEnumerable<LibraryDescription> packages, FrameworkName targetFramework)
        {
            Dependencies = packages;

            var cacheResolvers = GetCacheResolvers();
            var defaultResolver = new DefaultPackagePathResolver(_repository.RepositoryRoot);

            Servicing.Breadcrumbs breadcrumbs = new Servicing.Breadcrumbs(); 
            breadcrumbs.CreateRuntimeBreadcrumb(); 

            foreach (var dependency in packages)
            {
                var package = FindCandidate(dependency.Identity.Name, dependency.Identity.Version);

                if (package == null)
                {
                    continue;
                }

                string packagePath = ResolvePackagePath(defaultResolver, cacheResolvers, package);

                dependency.Path = packagePath;

                var packageDescription = new PackageDescription
                {
                    Library = dependency,
                    Package = package
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

                if (Servicing.Breadcrumbs.IsPackageServiceable(packageDescription.Package)) 
                { 
                    breadcrumbs.CreateBreadcrumb(package.Id, package.Version); 
                } 

                var assemblies = new List<string>();

                foreach (var assemblyInfo in GetPackageAssemblies(packageDescription, targetFramework))
                {
                    string replacementPath;
                    if (Servicing.ServicingTable.TryGetReplacement(
                        package.Id,
                        package.Version,
                        assemblyInfo.RelativePath,
                        out replacementPath))
                    {
                        _packageAssemblyLookup[assemblyInfo.Name] = new PackageAssembly()
                        {
                            Path = replacementPath,
                            RelativePath = assemblyInfo.RelativePath,
                            Library = dependency
                        };
                    }
                    else
                    {
                        _packageAssemblyLookup[assemblyInfo.Name] = new PackageAssembly()
                        {
                            Path = assemblyInfo.Path,
                            RelativePath = assemblyInfo.RelativePath,
                            Library = dependency
                        };
                    }
                    assemblies.Add(assemblyInfo.Name);
                }

                dependency.LoadableAssemblies = assemblies;
            }
        }

        private string ResolvePackagePath(IPackagePathResolver defaultResolver,
                                          IEnumerable<IPackagePathResolver> cacheResolvers,
                                          IPackage package)
        {
            var defaultHashPath = defaultResolver.GetHashPath(package.Id, package.Version);
            string expectedHash = null;
            if (File.Exists(defaultHashPath))
            {
                expectedHash = File.ReadAllText(defaultHashPath);
            }
            else if (_globalSettings != null)
            {
                var library = new Library()
                {
                    Name = package.Id,
                    Version = package.Version
                };

                _globalSettings.PackageHashes.TryGetValue(library, out expectedHash);
            }

            if (string.IsNullOrEmpty(expectedHash))
            {
                return defaultResolver.GetInstallPath(package.Id, package.Version);
            }

            foreach (var resolver in cacheResolvers)
            {
                var cacheHashFile = resolver.GetHashPath(package.Id, package.Version);

                // REVIEW: More efficient compare?
                if (File.Exists(cacheHashFile) &&
                    File.ReadAllText(cacheHashFile) == expectedHash)
                {
                    return resolver.GetInstallPath(package.Id, package.Version);
                }
            }

            return defaultResolver.GetInstallPath(package.Id, package.Version);
        }

        private static IEnumerable<IPackagePathResolver> GetCacheResolvers()
        {
            // TODO: remove KRE_ env var
            var packageCachePathValue = Environment.GetEnvironmentVariable("DOTNET_PACKAGES_CACHE") ?? Environment.GetEnvironmentVariable("KRE_PACKAGES_CACHE");

            if (string.IsNullOrEmpty(packageCachePathValue))
            {
                return Enumerable.Empty<IPackagePathResolver>();
            }

            return packageCachePathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(path => new DefaultPackagePathResolver(path));
        }

        public ILibraryExport GetLibraryExport(ILibraryKey target)
        {
            PackageDescription description;
            if (!_packageDescriptions.TryGetValue(target.Name, out description))
            {
                return null;
            }

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);

            PopulateMetadataReferences(description, target.TargetFramework, references);

            // REVIEW: This requires more design
            var sourceReferences = new List<ISourceReference>();

            foreach (var sharedSource in GetSharedSources(description, target.TargetFramework))
            {
                sourceReferences.Add(new SourceFileReference(sharedSource));
            }

            return new LibraryExport(references.Values.ToList(), sourceReferences);
        }

        private void PopulateMetadataReferences(PackageDescription description, FrameworkName targetFramework, IDictionary<string, IMetadataReference> paths)
        {
            var packageAssemblies = GetPackageAssemblies(description, targetFramework);

            // Use contract if both contract and target path are available
            bool hasContract = description.ContractPath != null;
            bool hasLib = packageAssemblies.Any();

            if (hasContract && hasLib && !VersionUtility.IsDesktop(targetFramework))
            {
                paths[description.Library.Identity.Name] = new MetadataFileReference(description.Library.Identity.Name, description.ContractPath);
            }
            else if (hasLib)
            {
                foreach (var assembly in packageAssemblies)
                {
                    paths[assembly.Name] = new MetadataFileReference(assembly.Name, assembly.Path);
                }
            }
        }


        private IEnumerable<string> GetSharedSources(PackageDescription description, FrameworkName targetFramework)
        {
            var directory = Path.Combine(description.Library.Path, "shared");
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories);
        }

        private static List<AssemblyDescription> GetPackageAssemblies(PackageDescription description, FrameworkName targetFramework)
        {
            var package = description.Package;
            var path = description.Library.Path;
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
                        Path = fileName,
                        RelativePath = reference.Path
                    });
                }
            }

            return results;
        }

        private IPackage FindCandidate(string name, SemanticVersion version)
        {
            return _repository.FindPackagesById(name).FirstOrDefault(p => p.Version == version)?.Package;
        }

        private IPackage FindCandidate(string name, SemanticVersionRange versionRange)
        {
            var packages = _repository.FindPackagesById(name);

            if (versionRange == null)
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
                    current: bestMatch?.Version,
                    considering: packageInfo.Version,
                    ideal: versionRange))
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
            // 1. DOTNET_PACKAGES environment variable
            // 2. global.json { "packages": "..." }
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. %userprofile%\.dotnet\packages

            // TODO: remove KRE_ env var
            var dotnetPackages = Environment.GetEnvironmentVariable("DOTNET_PACKAGES") ?? Environment.GetEnvironmentVariable("KRE_PACKAGES");

            if (!string.IsNullOrEmpty(dotnetPackages))
            {
                return dotnetPackages;
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

            return Path.Combine(profileDirectory, ".dotnet", "packages");
        }

        private class PackageDescription
        {
            public IPackage Package { get; set; }

            public LibraryDescription Library { get; set; }

            public string ContractPath { get; set; }
        }

        private class AssemblyDescription
        {
            public string Name { get; set; }

            public string Path { get; set; }

            public string RelativePath { get; set; }
        }
    }
}
