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
    public class PackageFeed : IPackageFeed
    {
        static readonly XName _xnameEntry = XName.Get("entry", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameTitle = XName.Get("title", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameContent = XName.Get("content", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameLink = XName.Get("link", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameProperties = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
        static readonly XName _xnameId = XName.Get("Id", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        static readonly XName _xnameVersion = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");

        private readonly string _baseUri;
        private readonly IReport _report;
        private HttpSource _httpSource;
        private TimeSpan _cacheAgeLimitList;
        private TimeSpan _cacheAgeLimitNupkg;

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

        Dictionary<string, Task<IEnumerable<PackageInfo>>> _cache = new Dictionary<string, Task<IEnumerable<PackageInfo>>>();
        Dictionary<string, Task<NupkgEntry>> _cache2 = new Dictionary<string, Task<NupkgEntry>>();

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

        public async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    var uri = _baseUri + "FindPackagesById()?Id='" + id + "'";
                    var results = new List<PackageInfo>();
                    var page = 1;
                    while (true)
                    {
                        // TODO: Pages for a package Id are cahced separately.
                        // So we will get inaccurate data when a page shrinks.
                        // However, (1) In most cases the pages grow rather than shrink;
                        // (2) cache for pages is valid for only 30 min.
                        // So we decide to leave current logic and observe.
                        using (var data = await _httpSource.GetAsync(uri,
                        string.Format("list_{0}_page{1}", id, page),
                        retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero))
                        {
                            var doc = XDocument.Load(data.Stream);

                            var result = doc.Root
                                .Elements(_xnameEntry)
                                .Select(x => BuildModel(id, x));

                            results.AddRange(result);

                            // Example of what this looks like in the odata feed:
                            // <link rel="next" href="{nextLink}" />
                            var nextUri = (from e in doc.Root.Elements(_xnameLink)
                                           let attr = e.Attribute("rel")
                                           where attr != null && string.Equals(attr.Value, "next", StringComparison.OrdinalIgnoreCase)
                                           select e.Attribute("href") into nextLink
                                           where nextLink != null
                                           select nextLink.Value).FirstOrDefault();

                            // Stop if there's nothing else to GET
                            if (string.IsNullOrEmpty(nextUri))
                            {
                                break;
                            }

                            uri = nextUri;
                            page++;
                        }
                    }

                    return results;
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        _report.WriteLine(string.Format("Error: FindPackagesById: {1}\r\n  {0}", ex.Message, id));
                        throw;
                    }
                    else
                    {
                        _report.WriteLine(string.Format("Warning: FindPackagesById: {1}\r\n  {0}", ex.Message, id));
                    }
                }
            }
            return null;
        }

        public PackageInfo BuildModel(string id, XElement element)
        {
            var properties = element.Element(_xnameProperties);

            return new PackageInfo
            {
                Id = element.Element(_xnameTitle).Value,
                Version = SemanticVersion.Parse(properties.Element(_xnameVersion).Value),
                ContentUri = element.Element(_xnameContent).Attribute("src").Value,
            };
        }

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
                        "nupkg_" + package.Id + "." + package.Version,
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
