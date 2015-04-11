// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.Client
{
    public class NuGetv2Feed : IPackageFeed
    {
        private static readonly XName _xnameEntry = XName.Get("entry", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameTitle = XName.Get("title", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameContent = XName.Get("content", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameLink = XName.Get("link", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameProperties = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
        private static readonly XName _xnameId = XName.Get("Id", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        private static readonly XName _xnameVersion = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");

        private readonly string _baseUri;
        private readonly HttpSource _httpSource;
        private readonly TimeSpan _cacheAgeLimitList;
        private readonly TimeSpan _cacheAgeLimitNupkg;
        private readonly bool _ignoreFailure;
        private bool _ignored;

        private readonly Dictionary<string, Task<IEnumerable<PackageInfo>>> _packageVersionsCache = new Dictionary<string, Task<IEnumerable<PackageInfo>>>();
        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>();

        public string Source { get; }

        private readonly ILogger _reports;

        public NuGetv2Feed(
            string baseUri,
            string userName,
            string password,
            bool noCache,
            bool ignoreFailure,
            ILogger report)
        {
            _reports = report;
            _baseUri = baseUri.EndsWith("/") ? baseUri : (baseUri + "/");
            _httpSource = new HttpSource(baseUri, userName, password, report);
            _ignoreFailure = ignoreFailure;
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
            Source = baseUri;
        }

        public Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id)
        {
            lock (_packageVersionsCache)
            {
                Task<IEnumerable<PackageInfo>> task;
                if (_packageVersionsCache.TryGetValue(id, out task))
                {
                    return task;
                }
                return _packageVersionsCache[id] = FindPackagesByIdAsyncCore(id);
            }
        }

        public async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                if (_ignored)
                {
                    return new List<PackageInfo>();
                }

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
                            try
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
                            catch (XmlException)
                            {
                                //_reports.Information.WriteLine("The XML file {0} is corrupt",
                                //    data.CacheFileName.Yellow().Bold());
                                throw;
                            }
                        }
                    }

                    return results;
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        // Fail silently by returning empty result list
                        if (_ignoreFailure)
                        {
                            _ignored = true;
                            _reports.WriteInformation(
                                string.Format("Failed to retrieve information from remote source '{0}'".Yellow(),
                                    _baseUri));
                            return new List<PackageInfo>();
                        }

                        _reports.WriteError(string.Format("Error: FindPackagesById: {1}\r\n  {0}",
                            ex.Message, id.Red().Bold()));
                        throw;
                    }
                    else
                    {
                        _reports.WriteInformation(string.Format("Warning: FindPackagesById: {1}\r\n  {0}", ex.Message, id.Yellow().Bold()));
                    }
                }
            }
            return null;
        }

        public PackageInfo BuildModel(string id, XElement element)
        {
            var properties = element.Element(_xnameProperties);
            var idElement = properties.Element(_xnameId);
            var titleElement = element.Element(_xnameTitle);

            return new PackageInfo
            {
                // If 'Id' element exist, use its value as accurate package Id
                // Otherwise, use the value of 'title' if it exist
                // Use the given Id as final fallback if all elements above don't exist
                Id = idElement?.Value ?? titleElement?.Value ?? id,
                Version = NuGetVersion.Parse(properties.Element(_xnameVersion).Value),
                ContentUri = element.Element(_xnameContent).Attribute("src").Value,
            };
        }

        public async Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            return await PackageUtilities.OpenNuspecStreamFromNupkgAsync(package, OpenNupkgStreamAsync, _reports);
        }

        public async Task<Stream> OpenNupkgStreamAsync(PackageInfo package)
        {
            Task<NupkgEntry> task;
            lock (_nupkgCache)
            {
                if (!_nupkgCache.TryGetValue(package.ContentUri, out task))
                {
                    task = _nupkgCache[package.ContentUri] = OpenNupkgStreamAsyncCore(package);
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

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(PackageInfo package)
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
                        _reports.WriteError(string.Format("Error: DownloadPackageAsync: {1}\r\n  {0}", ex.Message, package.ContentUri.Red().Bold()));
                    }
                    else
                    {
                        _reports.WriteInformation(string.Format("Warning: DownloadPackageAsync: {1}\r\n  {0}".Yellow().Bold(), ex.Message, package.ContentUri.Yellow().Bold()));
                    }
                }
            }
            return null;
        }

        private class NupkgEntry
        {
            public string TempFileName { get; set; }
        }
    }
}
