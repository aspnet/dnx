// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet;

namespace Microsoft.Dnx.Tooling.Restore.NuGet
{
    public class PackageFolder : IPackageFeed
    {
        private bool _ignored;
        private readonly bool _ignoreFailure;
        private readonly Reports _reports;
        private readonly PackageRepository _repository;
        private readonly IFileSystem _fileSystem;
        private readonly IPackagePathResolver _pathResolver;

        public string Source { get; }

        public PackageFolder(
            string physicalPath,
            bool ignoreFailure,
            Reports reports)
        {
            // We need to help restore operation to ensure case-sensitivity here
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
                message: string.Format("The local package source {0} doesn't exist", Source),
                fileName: Source);

            if (_ignoreFailure)
            {
                _reports.Information.WriteLine(string.Format("Warning: FindPackagesById: {1}\r\n  {0}",
                    exception.Message, id).Yellow().Bold());
                _ignored = true;
                return Task.FromResult(Enumerable.Empty<PackageInfo>());
            }

            _reports.Error.WriteLine(string.Format("Error: FindPackagesById: {1}\r\n  {0}",
                exception.Message, id).Red().Bold());
            throw exception;
        }

        public Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            var nuspecPath = _pathResolver.GetManifestFilePath(package.Id, package.Version);
            _reports.Quiet.WriteLine(string.Format("  OPEN {0}", _fileSystem.GetFullPath(nuspecPath)));
            return Task.FromResult<Stream>(File.OpenRead(nuspecPath));
        }

        public Task<Stream> OpenRuntimeStreamAsync(PackageInfo package)
        {
            var nuspecPath = _pathResolver.GetManifestFilePath(package.Id, package.Version);
            var runtimePath = Path.Combine(Path.GetDirectoryName(nuspecPath), "runtime.json");
            if (File.Exists(runtimePath))
            {
                _reports.Quiet.WriteLine(string.Format("  OPEN {0}", _fileSystem.GetFullPath(runtimePath)));
                return Task.FromResult<Stream>(File.OpenRead(runtimePath));
            }
            return Task.FromResult<Stream>(null);
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

