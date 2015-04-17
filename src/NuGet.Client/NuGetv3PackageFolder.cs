// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet;
using NuGet.Common;
using NuGet.Repositories;

namespace NuGet.Client
{
    public class NuGetv3PackageFolder : IPackageFeed
    {
        private bool _ignored;
        private readonly bool _ignoreFailure;
        private readonly NuGetv3LocalRepository _repository;
        private readonly ILogger _logger;

        public string Source { get; }

        public NuGetv3PackageFolder(
            string physicalPath,
            bool ignoreFailure,
            ILogger logger)
        {
            _logger = logger;
            // We need to help "kpm restore" to ensure case-sensitivity here
            // Turn on the flag to get package ids in accurate casing
            _repository = new NuGetv3LocalRepository(physicalPath, checkPackageIdCase: true);
            Source = physicalPath;
            _ignored = false;
            _ignoreFailure = ignoreFailure;
        }

        public Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id)
        {
            if (_ignored)
            {
                return Task.FromResult(Enumerable.Empty<PackageInfo>());
            }

            if (Directory.Exists(Source))
            {
                return Task.FromResult(_repository.FindPackagesById(id).Select(p => new PackageInfo
                {
                    Id = p.Id,
                    Version = p.Version,
                    ContentUri = p.ZipPath,
                    ManifestContentUri = p.ManifestPath
                }));
            }

            var exception = new FileNotFoundException(
                message: string.Format("The local package source {0} doesn't exist", Source.Bold()),
                fileName: Source);

            if (_ignoreFailure)
            {
                _logger.WriteInformation(string.Format("Warning: FindPackagesById: {1}\r\n  {0}",
                    exception.Message, id.Yellow().Bold()));

                _ignored = true;
                return Task.FromResult(Enumerable.Empty<PackageInfo>());
            }

            _logger.WriteError(string.Format("Error: FindPackagesById: {1}\r\n  {0}",
                exception.Message, id.Red().Bold()));
            throw exception;
        }

        public Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            var parentDir = Path.GetDirectoryName(package.ContentUri);
            var nuspecPath = Path.Combine(parentDir, $"{package.Id}.nuspec");
            _logger.WriteQuiet(string.Format("  OPEN {0}", nuspecPath));
            return Task.FromResult<Stream>(File.OpenRead(nuspecPath));
        }

        public Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            _logger.WriteQuiet(string.Format("  OPEN {0}", package.ContentUri));

            return Task.FromResult<Stream>(File.OpenRead(package.ContentUri));
        }
    }
}

