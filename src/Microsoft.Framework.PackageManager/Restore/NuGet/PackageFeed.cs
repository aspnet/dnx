// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public abstract class PackageFeed : IPackageFeed
    {
        protected readonly string _baseUri;
        protected readonly IReport _report;
        protected TimeSpan _cacheAgeLimitList;
        protected TimeSpan _cacheAgeLimitNupkg;
        internal HttpSource _httpSource;
        private Dictionary<string, Task<IEnumerable<PackageInfo>>> _cache =
            new Dictionary<string, Task<IEnumerable<PackageInfo>>>();
        private Dictionary<string, Task<NupkgEntry>> _cache2 =
            new Dictionary<string, Task<NupkgEntry>>();

        public PackageFeed(
            string baseUri,
            string userName,
            string password,
            bool noCache,
            IReport report)
        {
            _baseUri = baseUri.EndsWith("/") ? baseUri : (baseUri + "/");
            _report = report;
            _httpSource = new HttpSource(baseUri, userName, password, report);
            if (noCache)
            {
                _cacheAgeLimitList = TimeSpan.Zero;
                _cacheAgeLimitNupkg = TimeSpan.Zero;
            }
            else
            {
                _cacheAgeLimitList = TimeSpan.FromMinutes(30);
                _cacheAgeLimitNupkg = TimeSpan.FromHours(24);
            }
        }

        public Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id)
        {
            lock (_cache)
            {
                Task<IEnumerable<PackageInfo>> task;
                if (_cache.TryGetValue(id, out task))
                {
                    return task;
                }
                return _cache[id] = FindPackagesByIdAsyncCore(id);
            }
        }

        public abstract Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id);

        public async Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            using (var nupkgStream = await OpenNupkgStreamAsync(package))
            {
                using (var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var entry = archive.GetEntryOrdinalIgnoreCase(package.Id + ".nuspec");
                    using (var entryStream = entry.Open())
                    {
                        var nuspecStream = new MemoryStream((int)entry.Length);
#if NET45
                        await entryStream.CopyToAsync(nuspecStream);
#else
                        // System.IO.Compression.DeflateStream throws exception when multiple
                        // async readers/writers are working on a single instance of it
                        entryStream.CopyTo(nuspecStream);
#endif
                        nuspecStream.Seek(0, SeekOrigin.Begin);
                        return nuspecStream;
                    }
                }
            }
        }

        public async Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            Task<NupkgEntry> task;
            lock (_cache2)
            {
                if (!_cache2.TryGetValue(package.ContentUri, out task))
                {
                    task = _cache2[package.ContentUri] = _OpenNupkgStreamAsync(package);
                }
            }
            var result = await task;
            if (result == null)
            {
                return null;
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(result.TempFileName, _ =>
            {
                return Task.FromResult(
                    new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete));
            });
        }

        private async Task<NupkgEntry> _OpenNupkgStreamAsync(PackageInfo package)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await _httpSource.GetAsync(
                        package.ContentUri,
                        "nupkg_" + package.Id + "." + package.Version + "." + package.Configuration,
                        retry == 0 ? _cacheAgeLimitNupkg : TimeSpan.Zero))
                    {
                        return new NupkgEntry
                        {
                            TempFileName = data.CacheFileName
                        };
                    }
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        _report.WriteLine(string.Format("Error: DownloadPackageAsync: {1}\r\n  {0}", ex.Message, package.ContentUri.Red().Bold()));
                    }
                    else
                    {
                        _report.WriteLine(string.Format("Warning: DownloadPackageAsync: {1}\r\n  {0}".Yellow().Bold(), ex.Message, package.ContentUri.Yellow().Bold()));
                    }
                }
            }
            return null;
        }

        class NupkgEntry
        {
            public string TempFileName { get; set; }
        }
    }
}
