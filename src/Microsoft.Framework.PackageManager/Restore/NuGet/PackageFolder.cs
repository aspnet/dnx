// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public class PackageFolder : IPackageFeed
    {
        private IReport _report;
        private LocalPackageRepository _repository;

        public PackageFolder(
            string physicalPath,
            IReport report)
        {
            _repository = new LocalPackageRepository(physicalPath)
            {
                Report = report
            };
            _report = report;
        }

        public async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id)
        {
            return _repository.FindPackagesById(id).Select(p => new PackageInfo
            {
                Id = p.Id,
                Version = p.Version
            });
        }

        public async Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            using (var nupkgStream = await OpenNupkgStreamAsync(package))
            {
                if (PlatformHelper.IsMono)
                {
                    // Don't close the stream
                    var archive = Package.Open(nupkgStream, FileMode.Open, FileAccess.Read);
                    var partUri = PackUriHelper.CreatePartUri(new Uri(package.Id + ".nuspec", UriKind.Relative));
                    var entry = archive.GetPart(partUri);

                    using (var entryStream = entry.GetStream())
                    {
                        var nuspecStream = new MemoryStream((int)entryStream.Length);
                        await entryStream.CopyToAsync(nuspecStream);
                        nuspecStream.Seek(0, SeekOrigin.Begin);
                        return nuspecStream;
                    }
                }
                else
                {
                    using (var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, leaveOpen: true))
                    {
                        var entry = archive.GetEntry(package.Id + ".nuspec");
                        using (var entryStream = entry.Open())
                        {
                            var nuspecStream = new MemoryStream((int)entry.Length);
                            await entryStream.CopyToAsync(nuspecStream);
                            nuspecStream.Seek(0, SeekOrigin.Begin);
                            return nuspecStream;
                        }
                    }
                }
            }
        }

        public async Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            return _repository.FindPackage(package.Id, package.Version).GetStream();
        }
    }
}

