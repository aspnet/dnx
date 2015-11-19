// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet;

namespace Microsoft.Dnx.Tooling.Restore.NuGet
{
    public class NuGetPackageFolder : IPackageFeed
    {
        private readonly Reports _reports;
        private readonly LocalPackageRepository _repository;

        public string Source { get; }

        public NuGetPackageFolder(
            string physicalPath,
            Reports reports)
        {
            _repository = new LocalPackageRepository(physicalPath, reports.Quiet);
            _reports = reports;
            Source = physicalPath;
        }

        public Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id)
        {
            return Task.FromResult(_repository.FindPackagesById(id).Select(p => new PackageInfo
            {
                Id = p.Id,
                Version = p.Version
            }));
        }

        public async Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            return await PackageUtilities.OpenNuspecStreamFromNupkgAsync(package, OpenNupkgStreamAsync, _reports.Quiet);
        }

        public async Task<Stream> OpenRuntimeStreamAsync(PackageInfo package)
        {
            return await PackageUtilities.OpenRuntimeStreamFromNupkgAsync(package, OpenNupkgStreamAsync, _reports.Quiet);
        }

        public Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            return Task.FromResult(_repository.FindPackage(package.Id, package.Version).GetStream());
        }
    }
}

