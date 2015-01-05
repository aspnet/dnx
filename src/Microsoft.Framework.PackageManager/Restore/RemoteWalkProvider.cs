// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager
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

        public async Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework)
        {
            var results = await _source.FindPackagesByIdAsync(library.Name);
            PackageInfo bestResult = null;
            foreach (var result in results)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestResult?.Version,
                    considering: result.Version,
                    ideal: library.Version))
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
                Library = new Library { Name = bestResult.Id, Version = bestResult.Version },
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
                if (VersionUtility.TryGetCompatibleItems(targetFramework, metadata.DependencySets, out dependencySet))
                {
                    return dependencySet
                        .SelectMany(x => x.Dependencies)
                        .Select(x => new LibraryDependency(name: x.Id,
                            version: x.VersionSpec != null ? x.VersionSpec.MinVersion : null))
                        .ToList();
                }
            }
            return Enumerable.Empty<LibraryDependency>();
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

