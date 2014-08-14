// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore.NuGet
{
    public class V3PackageBlob : PackageFeed
    {
        private static readonly string BlobIndexFileName = "$index.json";
        public V3PackageBlob(
            string baseUri,
            bool noCache,
            IReport report) : base(baseUri, null, null, noCache, report)
        {
        }

        public override async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    var packageRootUri = _baseUri + id + "/";
                    var packageRootIndexUri = packageRootUri + BlobIndexFileName;
                    var results = new List<PackageInfo>();
                    using (var packageIndexData = await _httpSource.GetAsync(packageRootIndexUri,
                        string.Format("index_{0}", id),
                        retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero))
                    {
                        // There is no such a package
                        if (packageIndexData == null)
                        {
                            return results;
                        }

                        // $index.json at this level contains a list of version folders
                        var versions = JObject.Parse(packageIndexData.Stream.ReadToEnd())["Contents"].ToArray();
                        foreach (var version in versions)
                        {
                            var versionRootUri = packageRootUri + version + "/";
                            var versionRootIndexUri = versionRootUri + BlobIndexFileName;
                            using (var versionIndexData = await _httpSource.GetAsync(versionRootIndexUri,
                                string.Format("index_{0}.{1}", id, version),
                                retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero))
                            {
                                // It is not a valid version subfolder
                                if (versionIndexData == null)
                                {
                                    continue;
                                }

                                // $index.json at this level contains relative paths of files in its subfolders
                                var filePaths = JObject.Parse(versionIndexData.Stream.ReadToEnd())["Contents"];
                                var nupkgPaths = filePaths.Select(x => x.ToString())
                                    .Where(x => x.EndsWith(
                                        string.Format("{0}.{1}{2}", id, version, Constants.PackageExtension)));
                                foreach (var nupkgPath in nupkgPaths)
                                {
                                    var pathComponents = nupkgPath.Split(new[] { '/' }, 2);

                                    // Unknown format
                                    if (pathComponents.Length < 2)
                                    {
                                        continue;
                                    }

                                    results.Add(new PackageInfo()
                                    {
                                        Id = id,
                                        Version = SemanticVersion.Parse(version.ToString()),
                                        Configuration = pathComponents[0],
                                        ContentUri = versionRootUri + nupkgPath
                                    });
                                }
                            }
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
    }
}
