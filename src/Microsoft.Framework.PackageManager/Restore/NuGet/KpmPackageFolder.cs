// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public class KpmPackageFolder : IPackageFeed
    {
        private readonly IReport _report;
        private readonly PackageRepository _repository;
        private readonly IFileSystem _fileSystem;
        private readonly IPackagePathResolver _pathResolver;

        public KpmPackageFolder(
            string physicalPath,
            IReport report)
        {
            _repository = new PackageRepository(physicalPath);
            _fileSystem = new PhysicalFileSystem(physicalPath);
            _pathResolver = new DefaultPackagePathResolver(_fileSystem);
            _report = report;
        }

        public Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id)
        {
            return Task.FromResult(_repository.FindPackagesById(id).Select(p => new PackageInfo
            {
                Id = p.Id,
                Version = p.Version
            }));
        }

        public Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            var nuspecPath = _pathResolver.GetManifestFilePath(package.Id, package.Version);
            return Task.FromResult<Stream>(File.Open(nuspecPath, FileMode.Open));
        }

        public Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            var nuspecPath = _pathResolver.GetManifestFilePath(package.Id, package.Version);
            var unzippedPackage = new UnzippedPackage(_fileSystem, nuspecPath);

            var nupkgPath = _pathResolver.GetPackageFilePath(package.Id, package.Version);
            _report.WriteLine(string.Format("  OPEN {0}", _fileSystem.GetFullPath(nupkgPath)));

            return Task.FromResult(unzippedPackage.GetStream());
        }
    }
}

