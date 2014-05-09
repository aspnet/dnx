// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.PackageManager.Restore.NuGet;
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

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public class PackageFeed : IPackageFeed
    {
        static readonly XName _xnameEntry = XName.Get("entry", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameContent = XName.Get("content", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameProperties = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
        static readonly XName _xnameId = XName.Get("Id", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        static readonly XName _xnameVersion = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");

        private string _baseUri;
        private string _userName;
        private string _password;
        private IReport _report;

        public PackageFeed(
            string baseUri,
            string userName,
            string password,
            IReport report)
        {
            _baseUri = baseUri + (baseUri.EndsWith("/") ? "" : "/");
            _userName = userName;
            _password = password;
            _report = report;
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
                return _cache[id] = _FindPackagesByIdAsync(id);
            }
        }

        public async Task<IEnumerable<PackageInfo>> _FindPackagesByIdAsync(string id)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    var uri = _baseUri + "FindPackagesById()?Id='" + id + "'";

                    _report.WriteLine(string.Format("  {0} {1}", "GET".Yellow(), uri));

                    var response = await GetAsync(uri);
                    var stream = await response.Content.ReadAsStreamAsync();

                    _report.WriteLine(string.Format("  {1} {0} {2}ms", uri, response.StatusCode.ToString().Green(), sw.ElapsedMilliseconds.ToString().Bold()));

                    var doc = XDocument.Load(stream);
                    var result = doc.Root
                        .Elements(_xnameEntry)
                        .Select(x => BuildModel(id, x))
                        .ToArray();

                    return result;
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

        private async Task<HttpResponseMessage> GetAsync(string uri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (_userName != null)
            {
                var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(_userName + ":" + _password));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            };

            var client = new HttpClient();
            return await client.SendAsync(request);
        }

        public PackageInfo BuildModel(string id, XElement element)
        {
            var properties = element.Element(_xnameProperties);

            return new PackageInfo
            {
                Id = id,
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
            return new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        private async Task<NupkgEntry> _OpenNupkgStreamAsync(PackageInfo package)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    var result = new NupkgEntry();
                    result.TempFileName = Path.GetTempFileName();
                    result.TempFileStream = new FileStream(
                        result.TempFileName,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete,
                        8192 /*bufferSize*/,
                        FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                    _report.WriteLine(string.Format("  {0} {1}", "GET".Yellow(), package.ContentUri));

                    var response = await GetAsync(package.ContentUri);

                    await response.Content.CopyToAsync(result.TempFileStream);
                    await result.TempFileStream.FlushAsync();

                    _report.WriteLine(string.Format("  {1} {0} {2}ms", package.ContentUri, response.StatusCode.ToString().Green(), sw.ElapsedMilliseconds.ToString().Bold()));

                    sw.Stop();
                    return result;
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
            public FileStream TempFileStream { get; set; }
        }
    }
}

