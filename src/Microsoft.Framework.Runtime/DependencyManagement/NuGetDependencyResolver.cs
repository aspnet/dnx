// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Compilation;
using Microsoft.Framework.Runtime.DependencyManagement;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class NuGetDependencyResolver : IDependencyProvider, ILibraryExportProvider
    {
        private IDictionary<Tuple<string, FrameworkName, string, SemanticVersion>, LockFileTargetLibrary> _lookup;
        private readonly PackageRepository _repository;

        // Assembly name and path lifted from the appropriate lib folder
        private readonly Dictionary<string, PackageAssembly> _packageAssemblyLookup = new Dictionary<string, PackageAssembly>(StringComparer.OrdinalIgnoreCase);

        // All the information required by this package
        private readonly Dictionary<string, PackageDescription> _packageDescriptions = new Dictionary<string, PackageDescription>(StringComparer.OrdinalIgnoreCase);

        public NuGetDependencyResolver(PackageRepository repository)
        {
            _repository = repository;
            Dependencies = Enumerable.Empty<LibraryDescription>();
        }

        public IDictionary<string, PackageAssembly> PackageAssemblyLookup
        {
            get
            {
                return _packageAssemblyLookup;
            }
        }

        public IEnumerable<LibraryDescription> Dependencies { get; private set; }

        public void ApplyLockFile(LockFile lockFile)
        {
            _lookup = new Dictionary<Tuple<string, FrameworkName, string, SemanticVersion>, LockFileTargetLibrary>();

            foreach (var t in lockFile.Targets)
            {
                foreach (var library in t.Libraries)
                {
                    _lookup[Tuple.Create(t.RuntimeIdentifier, t.TargetFramework, library.Name, library.Version)] = library;
                }
            }

            _repository.ApplyLockFile(lockFile);
        }

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
                IEnumerable<LibraryDependency> dependencies;
                bool resolved = true;
                if (package.LockFileLibrary != null)
                {
                    var lookupKey = Tuple.Create((string)null, targetFramework, package.LockFileLibrary.Name, package.LockFileLibrary.Version);

                    LockFileTargetLibrary targetLibrary;
                    if (_lookup.TryGetValue(lookupKey, out targetLibrary))
                    {
                        dependencies = GetDependencies(package, targetFramework, targetLibrary);
                    }
                    else
                    {
                        resolved = false;
                        dependencies = Enumerable.Empty<LibraryDependency>();
                    }
                }
                else
                {
                    dependencies = GetDependencies(package, targetFramework, targetLibrary: null);
                }

                return new LibraryDescription
                {
                    LibraryRange = libraryRange,
                    Identity = new Library
                    {
                        Name = package.Id,
                        Version = package.Version
                    },
                    Type = "Package",
                    Dependencies = dependencies,
                    Resolved = resolved
                };
            }

            return null;
        }

        private IEnumerable<LibraryDependency> GetDependencies(PackageInfo packageInfo, FrameworkName targetFramework, LockFileTargetLibrary targetLibrary)
        {
            if (targetLibrary != null)
            {
                foreach (var d in targetLibrary.Dependencies)
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

                foreach (var fa in targetLibrary.FrameworkAssemblies)
                {
                    yield return new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = fa.AssemblyName,
                            IsGacOrFrameworkReference = true
                        }
                    };
                }

                yield break;
            }

            // If we weren't given a lockFileGroup, there isn't a lock file, so resolve the NuGet way.

            var package = packageInfo.Package;

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
            if (string.Equals(targetFramework.Identifier, VersionUtility.AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetFramework.Identifier, VersionUtility.DnxCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
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

        public void Initialize(IEnumerable<LibraryDescription> packages, FrameworkName targetFramework, string runtimeIdentifier)
        {
            Dependencies = packages;

            var cacheResolvers = GetCacheResolvers();
            var defaultResolver = new DefaultPackagePathResolver(_repository.RepositoryRoot);

            foreach (var dependency in packages)
            {
                var packageInfo = _repository.FindPackagesById(dependency.Identity.Name)
                    .FirstOrDefault(p => p.Version == dependency.Identity.Version);

                if (packageInfo == null)
                {
                    continue;
                }

                string packagePath = ResolvePackagePath(defaultResolver, cacheResolvers, packageInfo);

                // If the package path doesn't exist then mark this dependency as unresolved
                if (!Directory.Exists(packagePath))
                {
                    dependency.Resolved = false;
                    continue;
                }

                dependency.Path = packagePath;

                var packageDescription = new PackageDescription
                {
                    Library = dependency,
                    Package = packageInfo
                };

                _packageDescriptions[packageInfo.Id] = packageDescription;

                if (Servicing.Breadcrumbs.Instance.IsPackageServiceable(packageDescription.Package))
                {
                    Servicing.Breadcrumbs.Instance.AddBreadcrumb(packageInfo.Id, packageInfo.Version);
                }

                var lookupKey = Tuple.Create(runtimeIdentifier, targetFramework, packageInfo.LockFileLibrary.Name, packageInfo.LockFileLibrary.Version);

                if (_lookup == null)
                {
                    continue;
                }

                LockFileTargetLibrary targetLibrary;
                if (!_lookup.TryGetValue(lookupKey, out targetLibrary))
                {
                    continue;
                }

                var assemblies = new List<string>();

                foreach (var assemblyPath in targetLibrary.RuntimeAssemblies)
                {
                    var name = Path.GetFileNameWithoutExtension(assemblyPath);
                    var path = Path.Combine(dependency.Path, assemblyPath);

                    string replacementPath;
                    if (Servicing.ServicingTable.TryGetReplacement(
                        packageInfo.Id,
                        packageInfo.Version,
                        assemblyPath,
                        out replacementPath))
                    {
                        _packageAssemblyLookup[name] = new PackageAssembly()
                        {
                            Path = replacementPath,
                            RelativePath = assemblyPath,
                            Library = dependency
                        };
                    }
                    else
                    {
                        _packageAssemblyLookup[name] = new PackageAssembly()
                        {
                            Path = path,
                            RelativePath = assemblyPath,
                            Library = dependency
                        };
                    }

                    assemblies.Add(name);
                }

                dependency.LoadableAssemblies = assemblies;
            }
        }

        private string ResolvePackagePath(IPackagePathResolver defaultResolver,
                                          IEnumerable<IPackagePathResolver> cacheResolvers,
                                          PackageInfo packageInfo)
        {
            string expectedHash = packageInfo.LockFileLibrary.Sha512;

            foreach (var resolver in cacheResolvers)
            {
                var cacheHashFile = resolver.GetHashPath(packageInfo.Id, packageInfo.Version);

                // REVIEW: More efficient compare?
                if (File.Exists(cacheHashFile) &&
                    File.ReadAllText(cacheHashFile) == expectedHash)
                {
                    return resolver.GetInstallPath(packageInfo.Id, packageInfo.Version);
                }
            }

            return defaultResolver.GetInstallPath(packageInfo.Id, packageInfo.Version);
        }

        private static IEnumerable<IPackagePathResolver> GetCacheResolvers()
        {
            var packageCachePathValue = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesCache);

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

            if (!TryPopulateMetadataReferences(description, target.TargetFramework, references))
            {
                return null;
            }

            // REVIEW: This requires more design
            var sourceReferences = new List<ISourceReference>();

            foreach (var sharedSource in GetSharedSources(description, target.TargetFramework))
            {
                sourceReferences.Add(new SourceFileReference(sharedSource));
            }

            return new LibraryExport(references.Values.ToList(), sourceReferences);
        }

        private bool TryPopulateMetadataReferences(PackageDescription description, FrameworkName targetFramework, IDictionary<string, IMetadataReference> paths)
        {
            if (_lookup == null)
            {
                return false;
            }

            var lookupKey = Tuple.Create((string)null, targetFramework, description.Package.LockFileLibrary.Name, description.Package.LockFileLibrary.Version);

            LockFileTargetLibrary targetLibrary;
            if (!_lookup.TryGetValue(lookupKey, out targetLibrary))
            {
                return false;
            }

            foreach (var assemblyPath in targetLibrary.CompileTimeAssemblies)
            {
                var name = Path.GetFileNameWithoutExtension(assemblyPath);
                var path = Path.Combine(description.Library.Path, assemblyPath);
                paths[name] = new MetadataFileReference(name, path);
            }

            return true;
        }

        private IEnumerable<string> GetSharedSources(PackageDescription description, FrameworkName targetFramework)
        {
            var directory = Path.Combine(description.Library.Path, "shared");

            return description.Package.LockFileLibrary.Files.Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                                                            .Select(path => Path.Combine(description.Library.Path, path));
        }

        private IPackage FindCandidate(string name, SemanticVersion version)
        {
            return _repository.FindPackagesById(name).FirstOrDefault(p => p.Version == version)?.Package;
        }

        private PackageInfo FindCandidate(string name, SemanticVersionRange versionRange)
        {
            var packages = _repository.FindPackagesById(name);

            if (versionRange == null)
            {
                // TODO: Disallow null versions for nuget packages
                var packageInfo = packages.FirstOrDefault();
                if (packageInfo != null)
                {
                    return packageInfo;
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

            return bestMatch;
        }

        public static string ResolveRepositoryPath(string rootDirectory)
        {
            // Order
            // 1. EnvironmentNames.Packages environment variable
            // 2. global.json { "packages": "..." }
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.Packages);

            if (!string.IsNullOrEmpty(runtimePackages))
            {
                return runtimePackages;
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

            return Path.Combine(profileDirectory, Constants.DefaultLocalRuntimeHomeDir, "packages");
        }

        private class PackageDescription
        {
            public PackageInfo Package { get; set; }

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
