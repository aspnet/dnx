// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;

namespace Microsoft.Framework.PackageManager
{
    public class RemoteDependencyProvider : IRemoteDependencyProvider
    {
        private readonly IPackageFeed _source;

        public RemoteDependencyProvider(IPackageFeed source)
        {
            _source = source;
            IsHttp = IsHttpSource(source);
        }

        public bool IsHttp { get; private set; }

        public async Task<RemoteMatch> FindLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var results = await _source.FindPackagesByIdAsync(libraryRange.Name);
            PackageInfo bestResult = null;
            foreach (var result in results)
            {
                if (libraryRange.VersionRange.IsBetter(
                    current: bestResult?.Version,
                    considering: result.Version))
                {
                    bestResult = result;
                }
            }

            if (bestResult == null)
            {
                return null;
            }

            return new RemoteMatch
            {
                Library = new LibraryIdentity
                {
                    Name = bestResult.Id,
                    Version = bestResult.Version,
                    Type = LibraryTypes.Package
                },
                Path = bestResult.ContentUri,
                Provider = this,
            };
        }

        public async Task<IEnumerable<LibraryDependency>> GetDependencies(RemoteMatch match, NuGetFramework targetFramework)
        {
            using (var stream = await _source.OpenNuspecStreamAsync(new PackageInfo
            {
                Id = match.Library.Name,
                Version = match.Library.Version,
                ContentUri = match.Path
            }))
            {
                var nuspecReader = new NuspecReader(stream);
                var reducer = new FrameworkReducer();

                var groups = nuspecReader.GetDependencyGroups()
                                         .ToDictionary(g => g.TargetFramework,
                                                       g => g.Packages);


                var nearest = reducer.GetNearest(targetFramework, groups.Keys);

                if (nearest != null)
                {
                    return groups[nearest].Select(p => new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = p.Id,
                            VersionRange = p.VersionRange
                        }
                    })
                    .ToList();
                }
            }

            return Enumerable.Empty<LibraryDependency>();
        }

        public async Task CopyToAsync(RemoteMatch match, Stream stream)
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

