// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public class NuGetv2PackageFolder : IPackageFeed
    {
        private readonly NuGetv2LocalRepository _repository;
        private readonly ILogger _logger;

        public string Source { get; }

        public NuGetv2PackageFolder(string physicalPath, ILogger logger)
        {
            _repository = new NuGetv2LocalRepository(physicalPath);
            _logger = logger;

            Source = physicalPath;
        }

        public Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id)
        {
            return Task.FromResult(_repository.FindPackagesById(id).Select(p => new PackageInfo
            {
                Id = p.Id,
                Version = p.Version,
                ContentUri = p.ZipPath,
                // This is null
                ManifestContentUri = p.ManifestPath
            }));
        }

        public async Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            _logger.WriteQuiet(string.Format("  OPEN {0}", package.ContentUri));
            return await PackageUtilities.OpenNuspecStreamFromNupkgAsync(package, OpenNupkgStreamAsync, _logger);
        }

        public Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            _logger.WriteQuiet(string.Format("  OPEN {0}", package.ContentUri));
            return Task.FromResult<Stream>(File.OpenRead(package.ContentUri));
        }
    }
}

