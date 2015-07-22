// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Dnx.Runtime.Internal;
using NuGet;

namespace Microsoft.Dnx.Tooling.Restore.NuGet
{
    public class NuGetv2Feed : IPackageFeed
    {
        private static readonly XNamespace _defaultNamespace = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace _odataMetadataNamespace = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        private static readonly XNamespace _odataNamespace = "http://schemas.microsoft.com/ado/2007/08/dataservices";
        private static readonly XName _xnameEntry = _defaultNamespace + "entry";
        private static readonly XName _xnameTitle = _defaultNamespace + "title";
        private static readonly XName _xnameContent = _defaultNamespace + "content";
        private static readonly XName _xnameLink = _defaultNamespace + "link";
        private static readonly XName _xnameProperties = _odataMetadataNamespace + "properties";
        private static readonly XName _xnameId = _odataNamespace + "Id";
        private static readonly XName _xnameVersion = _odataNamespace + "Version";
        private static readonly XName _xnamePublish = _odataNamespace + "Published";

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

        public string Source
        {
            get
            {
                return _httpSource.BaseUri;
            }
        }

        internal NuGetv2Feed(
            HttpSource httpSource,
            bool noCache,
            Reports reports,
            bool ignoreFailure)
        {
            _baseUri = httpSource.BaseUri;
            _reports = reports;
            _httpSource = httpSource;
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
                    var uri = $"{_baseUri}FindPackagesById()?id='{id}'";
                    var results = new List<PackageInfo>();
                    var page = 1;
                    while (true)
                    {
                        // TODO: Pages for a package Id are cached separately.
                        // So we will get inaccurate data when a page shrinks.
                        // However, (1) In most cases the pages grow rather than shrink;
                        // (2) cache for pages is valid for only 30 min.
                        // So we decide to leave current logic and observe.
                        using (var data = await _httpSource.GetAsync(uri,
                        cacheKey: $"list_{id}_page{page}",
                        cacheAgeLimit: retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero,
                        ensureValidContents: stream => EnsureValidFindPackagesResponse(stream, uri)))
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
                                _reports.Information.WriteLine(
                                    $"XML file {data.CacheFileName} is corrupt".Yellow().Bold());
                                throw;
                            }
                        }
                    }

                    return results;
                }
                catch (Exception ex)
                {
                    var isFinalAttempt = (retry == 2);
                    var message = ex.Message;
                    if (ex is TaskCanceledException)
                    {
                        message = ErrorMessageUtils.GetFriendlyTimeoutErrorMessage(
                            ex as TaskCanceledException,
                            isFinalAttempt,
                            _ignoreFailure);
                    }

                    if (isFinalAttempt)
                    {
                        // Fail silently by returning empty result list
                        if (_ignoreFailure)
                        {
                            _ignored = true;
                            _reports.Information.WriteLine(
                                $"Warning: FindPackagesById: {id}{Environment.NewLine}  {message}".Yellow().Bold());
                            return new List<PackageInfo>();
                        }

                        _reports.Error.WriteLine(
                            $"Error: FindPackagesById: {id}{Environment.NewLine}  {message}".Red().Bold());
                        throw;
                    }
                    else
                    {
                        _reports.Information.WriteLine(
                            $"Warning: FindPackagesById: {id}{Environment.NewLine}  {message}".Yellow().Bold());
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

            var listed = true;
            if (publishElement != null)
            {
                DateTime publishDate;
                if (DateTime.TryParse(publishElement.Value, out publishDate) && (publishDate == _unlistedPublishedTime))
                {
                    listed = false;
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
                Listed = listed
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
                    task = _nupkgCache[package.ContentUri] = PackageUtilities.OpenNupkgStreamAsync(
                        _httpSource, package, _cacheAgeLimitNupkg, _reports);
                }
            }
            var result = await task;
            if (result == null)
            {
                return null;
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(result.TempFileName, action: _ =>
            {
                return Task.FromResult(
                    new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete));
            });
        }

        private static void EnsureValidFindPackagesResponse(Stream stream, string uri)
        {
            var message = $"Response from {uri} is not a valid NuGet v2 service response.";
            try
            {
                var xDoc = XDocument.Load(stream);
                if (!_defaultNamespace.Equals(xDoc.Root.Name.Namespace))
                {
                    throw new InvalidDataException(
                        $"{message} Namespace of root element is not {_defaultNamespace.NamespaceName}.");
                }
            }
            catch (XmlException e)
            {
                throw new InvalidDataException(message, innerException: e);
            }
        }
    }
}
