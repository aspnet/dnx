using NuGet;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using System.IO;

namespace Microsoft.Framework.PackageManager
{
    public class RemoteWalkProvider : IWalkProvider
    {
        private IPackageFeed _source;

        public RemoteWalkProvider(IPackageFeed source)
        {
            _source = source;
        }

        public async Task<WalkProviderMatch> FindLibraryByName(string name, FrameworkName targetFramework)
        {
            return null;
        }

        public async Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework)
        {
            return await FindLibraryBySnapshot(library, targetFramework);
        }

        public async Task<WalkProviderMatch> FindLibraryBySnapshot(Library library, FrameworkName targetFramework)
        {
            var results = await _source.FindPackagesByIdAsync(library.Name);
            PackageInfo bestResult = null;
            foreach (var result in results)
            {
                if (result.Version.EqualsSnapshot(library.Version))
                {
                    if (bestResult == null || bestResult.Version < result.Version)
                    {
                        bestResult = result;
                    }
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

        public async Task<IEnumerable<Library>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework)
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
                        .Select(x => new Library { Name = x.Id, Version = x.VersionSpec.MinVersion })
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
                ContentUri = match.Path
            }))
            {
                await nupkgStream.CopyToAsync(stream);
            }
        }
    }
}

