// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public class KpmPackageFolder : IPackageFeed
    {
        private bool _ignored;
        private readonly bool _ignoreFailure;
        private readonly Reports _reports;
        private readonly PackageRepository _repository;
        private readonly IFileSystem _fileSystem;
        private readonly IPackagePathResolver _pathResolver;

        public string Source { get; }

        public KpmPackageFolder(
            string physicalPath,
            bool ignoreFailure,
            Reports reports)
        {
            // We need to help "kpm restore" to ensure case-sensitivity here
            // Turn on the flag to get package ids in accurate casing
            _repository = new PackageRepository(physicalPath, caseSensitivePackagesName: true);
            _fileSystem = new PhysicalFileSystem(physicalPath);
            _pathResolver = new DefaultPackagePathResolver(_fileSystem);
            _reports = reports;
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
                    Version = p.Version
                }));
            }

            var exception = new FileNotFoundException(
                message: string.Format("The local package source {0} doesn't exist", Source.Bold()),
                fileName: Source);

            if (_ignoreFailure)
            {
                _reports.Information.WriteLine(string.Format("Warning: FindPackagesById: {1}\r\n  {0}",
                    exception.Message, id.Yellow().Bold()));
                _ignored = true;
                return Task.FromResult(Enumerable.Empty<PackageInfo>());
            }

            _reports.Error.WriteLine(string.Format("Error: FindPackagesById: {1}\r\n  {0}",
                exception.Message, id.Red().Bold()));
            throw exception;
        }

        public Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            var nuspecPath = _pathResolver.GetManifestFilePath(package.Id, package.Version);
            _reports.Quiet.WriteLine(string.Format("  OPEN {0}", _fileSystem.GetFullPath(nuspecPath)));
            return Task.FromResult<Stream>(File.OpenRead(nuspecPath));
        }

        public Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            var nuspecPath = _pathResolver.GetManifestFilePath(package.Id, package.Version);
            var unzippedPackage = new UnzippedPackage(_fileSystem, nuspecPath);

            var nupkgPath = _pathResolver.GetPackageFilePath(package.Id, package.Version);
            _reports.Quiet.WriteLine(string.Format("  OPEN {0}", _fileSystem.GetFullPath(nupkgPath)));

            return Task.FromResult(unzippedPackage.GetStream());
        }
    }
}

