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
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    internal class NuGetv3Feed : IPackageFeed
    {
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

        internal NuGetv3Feed(
            HttpSource httpSource,
            bool noCache,
            Reports reports,
            bool ignoreFailure)
        {
            var baseUri = httpSource.BaseUri;
            _baseUri = baseUri.EndsWith("/index.json") ? baseUri.Substring(0, baseUri.Length - "index.json".Length) : baseUri;
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
            Source = baseUri;
        }

        internal static bool DetectNuGetV3(HttpSource httpSource, out Uri packageBaseAddress)
        {
            try
            {
                var result = httpSource.GetAsync(httpSource.BaseUri, "index_json", TimeSpan.FromHours(6)).Result;
                using (var reader = new StreamReader(result.Stream))
                {
                    var content = reader.ReadToEnd();
                    var indexJson = JObject.Parse(content);
                    foreach (var resource in indexJson["resources"])
                    {
                        var type = resource.Value<string>("@type");
                        var id = resource.Value<string>("@id");

                        if (id != null && string.Equals(type, "PackageBaseAddress/3.0.0"))
                        {
                            try
                            {
                                packageBaseAddress = new Uri(id);
                                return true;
                            }
                            catch (UriFormatException)
                            {
                                packageBaseAddress = new Uri(new Uri(httpSource.BaseUri), id);
                                return true;
                            }
                        }
                    }
                }
                packageBaseAddress = null;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            packageBaseAddress = null;
            return false;
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
                    var uri = _baseUri + id.ToLowerInvariant() + "/index.json";
                    var results = new List<PackageInfo>();
                    using (var data = await _httpSource.GetAsync(uri,
                        string.Format("list_{0}", id),
                        retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero,
                        throwNotFound: false))
                    {
                        if (data.Stream == null)
                        {
                            return Enumerable.Empty<PackageInfo>();
                        }

                        try
                        {
                            JObject doc;
                            using (var reader = new StreamReader(data.Stream))
                            {
                                doc = JObject.Load(new JsonTextReader(reader));
                            }

                            var result = doc["versions"]
                                .Select(x => BuildModel(id, x.ToString()))
                                .Where(x => x != null);

                            results.AddRange(result);
                        }
                        catch
                        {
                            _reports.Information.WriteLine("The file {0} is corrupt",
                                data.CacheFileName.Yellow().Bold());
                            throw;
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

        public PackageInfo BuildModel(string id, string version)
        {
            return new PackageInfo
            {
                // If 'Id' element exist, use its value as accurate package Id
                // Otherwise, use the value of 'title' if it exist
                // Use the given Id as final fallback if all elements above don't exist
                Id = id,
                Version = SemanticVersion.Parse(version),
                ContentUri = _baseUri + id.ToLowerInvariant() + "/" + version.ToLowerInvariant() + "/" + id.ToLowerInvariant() + "." + version.ToLowerInvariant() + ".nupkg",
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
