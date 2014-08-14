// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        private IPackageFeed _source;

        public RemoteWalkProvider(IPackageFeed source)
        {
            _source = source;
        }

        public Task<WalkProviderMatch> FindLibraryByName(string name, string configuration, FrameworkName targetFramework)
        {
            return Task.FromResult<WalkProviderMatch>(null);
        }

        public async Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework)
        {
            return await FindLibraryBySnapshot(library, targetFramework);
        }

        public async Task<WalkProviderMatch> FindLibraryBySnapshot(Library library, FrameworkName targetFramework)
        {
            var results = await _source.FindPackagesByIdAsync(library.Name);
            results = results.Where(x => string.Equals(library.Configuration, x.Configuration));
            PackageInfo bestResult = null;
            foreach (var result in results)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestResult == null ? null : bestResult.Version,
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
                Library = new Library { Name = bestResult.Id, Version = bestResult.Version,
                    Configuration = library.Configuration },
                Path = bestResult.ContentUri,
                Provider = this,
            };
        }

        public async Task<IEnumerable<Library>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework)
        {
            using (var stream = await _source.OpenNuspecStreamAsync(new PackageInfo
            {
                Id = match.Library.Name,
                Version = match.Library.Version,
                Configuration = match.Library.Configuration,
                ContentUri = match.Path
            }))
            {
                var metadata = (IPackageMetadata)Manifest.ReadFrom(stream, validateSchema: false).Metadata;
                IEnumerable<PackageDependencySet> dependencySet;
                if (VersionUtility.TryGetCompatibleItems(targetFramework, metadata.DependencySets, out dependencySet))
                {
                    return dependencySet
                        .SelectMany(x => x.Dependencies)
                        .Select(x => new Library { Name = x.Id,
                            Version = x.VersionSpec != null ? x.VersionSpec.MinVersion : null,
                            Configuration = match.Library.Configuration })
                        .ToList();
                }
            }
            return Enumerable.Empty<Library>();
        }

        public async Task CopyToAsync(WalkProviderMatch match, Stream stream)
        {
            using (var nupkgStream = await _source.OpenNupkgStreamAsync(new PackageInfo
            {
                Id = match.Library.Name,
                Version = match.Library.Version,
                Configuration = match.Library.Configuration,
                ContentUri = match.Path
            }))
            {
                await nupkgStream.CopyToAsync(stream);
            }
        }
    }
}

