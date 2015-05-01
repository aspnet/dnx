// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public class NuGetv2Feed : IPackageFeed
    {
        private static readonly string _defaultNamespace = "http://www.w3.org/2005/Atom";
        private static readonly XName _xnameEntry = XName.Get("entry", _defaultNamespace);
        private static readonly XName _xnameTitle = XName.Get("title", _defaultNamespace);
        private static readonly XName _xnameContent = XName.Get("content", _defaultNamespace);
        private static readonly XName _xnameLink = XName.Get("link", _defaultNamespace);
        private static readonly XName _xnameProperties = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
        private static readonly XName _xnameId = XName.Get("Id", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        private static readonly XName _xnameVersion = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        private static readonly XName _xnamePublish = XName.Get("Published", "http://schemas.microsoft.com/ado/2007/08/dataservices");

        // An unlisted package's publish time must be 1900-01-01T00:00:00.
        private static readonly DateTime _unlistedPublishedTime = new DateTime(1900, 1, 1, 0, 0, 0);

        private readonly string _baseUri;
        private readonly Reports _reports;
        private readonly HttpSource _httpSource;
        private readonly TimeSpan _cacheAgeLimitList;
        private readonly TimeSpan _cacheAgeLimitNupkg;
        private readonly bool _ignoreFailure;
        private bool _ignored;

        private readonly Dictionary<string, Task<IEnumerable<PackageInfo>>> _packageVersionsCache = new Dictionary<string, Task<IEnumerable<PackageInfo>>>();
        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>();

        public string Source { get; }

        public NuGetv2Feed(
            string baseUri,
            string userName,
            string password,
            bool noCache,
            Reports reports,
            bool ignoreFailure)
        {
            _baseUri = baseUri.EndsWith("/") ? baseUri : (baseUri + "/");
            _reports = reports;
            _httpSource = new HttpSource(baseUri, userName, password, reports);
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
                    var cacheKey = string.Format("list_{0}_page{1}", id, page);
                    while (true)
                    {
                        // TODO: Pages for a package Id are cached separately.
                        // So we will get inaccurate data when a page shrinks.
                        // However, (1) In most cases the pages grow rather than shrink;
                        // (2) cache for pages is valid for only 30 min.
                        // So we decide to leave current logic and observe.
                        using (var data = await _httpSource.GetAsync(
                            uri,
                            cacheKey,
                            retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero))
                        {
                            try
                            {
                                var doc = XDocument.Load(data.Stream);
                                EnsureValidFindPackagesResponse(doc);

                                var result = doc.Root
                                    .Elements(_xnameEntry)
                                    .Select(x => BuildModel(id, x))
                                    .Where(x => x != null);

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
                            catch (FormatException)
                            {
                                await HandleInvalidFindPackagesResponse(uri, cacheKey);
                                throw;
                            }
                            catch (XmlException)
                            {
                                await HandleInvalidFindPackagesResponse(uri, cacheKey);
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
                            _reports.Information.WriteLine(
                                string.Format("Failed to retrieve information from remote source '{0}'",
                                    _baseUri).Yellow().Bold());
                            return new List<PackageInfo>();
                        }

                        _reports.Error.WriteLine(string.Format("Error: FindPackagesById: {1}\r\n  {0}",
                            ex.Message, id).Red().Bold());
                        throw;
                    }
                    else
                    {
                        _reports.Information.WriteLine(string.Format("Warning: FindPackagesById: {1}\r\n  {0}", ex.Message, id).Yellow().Bold());
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

            var publishElement = properties.Element(_xnamePublish);
            if (publishElement != null)
            {
                DateTime publishDate; 
                if (DateTime.TryParse(publishElement.Value, out publishDate) && (publishDate == _unlistedPublishedTime))
                {
                    return null; 
                }
            }

            return new PackageInfo
            {
                // If 'Id' element exist, use its value as accurate package Id
                // Otherwise, use the value of 'title' if it exist
                // Use the given Id as final fallback if all elements above don't exist
                Id = idElement?.Value ?? titleElement?.Value ?? id,
                Version = SemanticVersion.Parse(properties.Element(_xnameVersion).Value),
                ContentUri = element.Element(_xnameContent).Attribute("src").Value,
            };
        }

        public async Task<Stream> OpenNuspecStreamAsync(PackageInfo package)
        {
            return await PackageUtilities.OpenNuspecStreamFromNupkgAsync(package, OpenNupkgStreamAsync, _reports.Information);
        }

        public async Task<Stream> OpenRuntimeStreamAsync(PackageInfo package)
        {
            return await PackageUtilities.OpenRuntimeStreamFromNupkgAsync(package, OpenNupkgStreamAsync, _reports.Information);
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

            // Acquire the lock on a file before we open it to prevent this process from opening
            // a file deleted by HttpSource.GetAsync()/HttpSource.InvalidateCacheFile() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(result.TempFileName, _ =>
            {
                return Task.FromResult(
                    new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete));
            });
        }

        private void EnsureValidFindPackagesResponse(XDocument doc)
        {
            if (!string.Equals(doc.Root.GetDefaultNamespace().ToString(), _defaultNamespace,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("The XML is not a valid NuGet v2 service response to FindPackagesById() request");
            }
        }

        private async Task HandleInvalidFindPackagesResponse(string uri, string cacheKey)
        {
            await _httpSource.InvalidateCacheFileAsync(cacheKey);
            _reports.Information.WriteLine(
                "The response from {0} has invalid format. Invalidating the cached file.",
                uri.Yellow().Bold());
        }

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(PackageInfo package)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    var cacheKey = "nupkg_" + package.Id + "." + package.Version;
                    using (var data = await _httpSource.GetAsync(
                        package.ContentUri,
                        cacheKey,
                        retry == 0 ? _cacheAgeLimitNupkg : TimeSpan.Zero))
                    {
                        var isValidNupkg = await PackageUtilities.IsValidNupkgAsync(package.Id, data.CacheFileName);
                        if (!isValidNupkg)
                        {
                            await _httpSource.InvalidateCacheFileAsync(cacheKey);

                            _reports.Information.WriteLine(
                                "The response from {0} is not a valid NuGet package. Invalidating the cached file.",
                                package.ContentUri.Yellow().Bold());

                            var message = string.Format("{0} is not a valid ZIP archive of NuGet package",
                                data.CacheFileName);
                            throw new InvalidDataException(message);
                        }

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
                        _reports.Error.WriteLine(string.Format("Error: DownloadPackageAsync: {1}\r\n  {0}", ex.Message, package.ContentUri.Red().Bold()));
                    }
                    else
                    {
                        _reports.Information.WriteLine(string.Format("Warning: DownloadPackageAsync: {1}\r\n  {0}".Yellow().Bold(), ex.Message, package.ContentUri.Yellow().Bold()));
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
