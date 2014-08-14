// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public class V2PackageFeed : PackageFeed
    {
        static readonly XName _xnameEntry = XName.Get("entry", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameContent = XName.Get("content", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameLink = XName.Get("link", "http://www.w3.org/2005/Atom");
        static readonly XName _xnameProperties = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
        static readonly XName _xnameId = XName.Get("Id", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        static readonly XName _xnameVersion = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");

        public V2PackageFeed(
            string baseUri,
            string userName,
            string password,
            bool noCache,
            IReport report) : base(baseUri, userName, password, noCache, report)
        {
            _httpSource = new HttpSource(baseUri, userName, password, report);
        }

        public override async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id)
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
                Id = id,
                Version = SemanticVersion.Parse(properties.Element(_xnameVersion).Value),
                ContentUri = element.Element(_xnameContent).Attribute("src").Value,
            };
        }
    }
}
