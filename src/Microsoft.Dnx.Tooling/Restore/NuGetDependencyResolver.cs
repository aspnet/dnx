// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class NuGetDependencyResolver : IDependencyProvider
    {
        private readonly PackageRepository _repository;

        public NuGetDependencyResolver(PackageRepository repository)
        {
            _repository = repository;
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

            var versionRange = libraryRange.VersionRange;

            var package = FindCandidate(libraryRange.Name, versionRange);

            if (package != null)
            {
                var dependencies = GetDependencies(package, targetFramework);

                return new LibraryDescription(
                    libraryRange,
                    new LibraryIdentity(package.Id, package.Version, isGacOrFrameworkReference: false),
                    path: null,
                    type: LibraryTypes.Package,
                    dependencies: dependencies,
                    assemblies: null,
                    framework: null);
            }

            return null;
        }

        private IEnumerable<LibraryDependency> GetDependencies(PackageInfo packageInfo, FrameworkName targetFramework)
        {
            var package = packageInfo.Package;

            IEnumerable<PackageDependencySet> dependencySet;
            if (VersionUtility.GetNearest(targetFramework, package.DependencySets, out dependencySet))
            {
                foreach (var set in dependencySet)
                {
                    foreach (var d in set.Dependencies)
                    {
                        yield return new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(d.Id, frameworkReference: false)
                            {
                                VersionRange = d.VersionSpec == null ? null : new SemanticVersionRange(d.VersionSpec)
                            },
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
            if (VersionUtility.GetNearest(targetFramework, package.FrameworkAssemblies, out frameworkAssemblies))
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
                        LibraryRange = new LibraryRange(assemblyReference.AssemblyName, frameworkReference: true)
                    };
                }
            }
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
            return PackageDependencyProvider.ResolveRepositoryPath(rootDirectory);
        }
    }
}
