using Microsoft.Net.PackageManager.Restore.NuGet;
using NuGet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using System.Linq;

namespace Microsoft.Net.PackageManager.Restore.NuGet
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
            return _repository.FindPackagesById(id).Select(p=>new PackageInfo
            {
                Id=p.Id,
                Version=p.Version
            });
        }

        public async Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            using (var nupkgStream = await OpenNupkgStreamAsync(package))
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

        public async Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            return _repository.FindPackage(package.Id, package.Version).GetStream();
        }
    }
}

