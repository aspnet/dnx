// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Tooling.Restore.NuGet;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class RemoteWalkProvider : IWalkProvider
    {
        private readonly IPackageFeed _source;

        public RemoteWalkProvider(IPackageFeed source)
        {
            _source = source;
            IsHttp = IsHttpSource(source);
        }

        public bool IsHttp { get; private set; }

        public async Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, FrameworkName targetFramework, bool includeUnlisted)
        {
            if (!DependencyTargets.SupportsPackage(libraryRange.Target))
            {
                return null;
            }

            var results = await _source.FindPackagesByIdAsync(libraryRange.Name);
            PackageInfo bestResult = null;
            if (!includeUnlisted)
            {
                results = results.Where(p => p.Listed);
            }

            foreach (var result in results)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestResult?.Version,
                    considering: result.Version,
                    ideal: libraryRange.VersionRange))
                {
                    bestResult = result;
                }
            }

            if (bestResult == null)
            {
                return null;
            }

            return new WalkProviderMatch
            {
                Library = new LibraryIdentity(bestResult.Id, bestResult.Version, isGacOrFrameworkReference: false),
                LibraryType = Runtime.LibraryTypes.Package,
                Path = bestResult.ContentUri,
                Provider = this,
            };
        }

        public async Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework)
        {
            using (var stream = await _source.OpenNuspecStreamAsync(new PackageInfo
            {
                Id = match.Library.Name,
                Version = match.Library.Version,
                ContentUri = match.Path
            }))
            {
                var metadata = (IPackageMetadata)Manifest.ReadFrom(stream, validateSchema: false).Metadata;
                IEnumerable<PackageDependencySet> dependencySet;
                if (VersionUtility.GetNearest(targetFramework, metadata.DependencySets, out dependencySet))
                {
                    return dependencySet
                        .SelectMany(ds => ds.Dependencies)
                        .Select(d => new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(d.Id, frameworkReference: false)
                            {
                                VersionRange = d.VersionSpec == null ? null : new SemanticVersionRange(d.VersionSpec)
                            }
                        })
                        .ToList();
                }
            }
            return Enumerable.Empty<LibraryDependency>();
        }

        public async Task<RuntimeFile> GetRuntimes(WalkProviderMatch match, FrameworkName targetFramework)
        {
            using (var stream = await _source.OpenRuntimeStreamAsync(new PackageInfo
            {
                Id = match.Library.Name,
                Version = match.Library.Version,
                ContentUri = match.Path
            }))
            {
                if (stream != null)
                {
                    var formatter = new RuntimeFileFormatter();
                    using (var reader = new StreamReader(stream))
                    {
                        return formatter.ReadRuntimeFile(reader);
                    }
                }
            }
            return null;
        }

        public async Task CopyToAsync(WalkProviderMatch match, Stream stream)
        {
            using (var nupkgStream = await _source.OpenNupkgStreamAsync(new PackageInfo
            {
                Id = match.Library.Name,
                Version = match.Library.Version,
                ContentUri = match.Path
            }))
            {
                await nupkgStream.CopyToAsync(stream);
            }
        }

        private static bool IsHttpSource(IPackageFeed source)
        {
            return source.Source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                source.Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}

