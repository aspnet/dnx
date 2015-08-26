// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Dnx.Tooling.Restore.NuGet;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class PackageFeedCache
    {
        private readonly ConcurrentDictionary<PackageSource, IPackageFeed> _packageFeeds = new ConcurrentDictionary<PackageSource, IPackageFeed>();

        public IPackageFeed GetPackageFeed(PackageSource source, bool noCache, bool ignoreFailedSources,
            Reports reports)
        {
            return _packageFeeds.GetOrAdd(source, _ => CreatePackageFeed(source, noCache, ignoreFailedSources, reports));
        }

        private IPackageFeed CreatePackageFeed(PackageSource source, bool noCache, bool ignoreFailedSources,
            Reports reports)
        {
            // Check if the feed is a file path
            if (source.IsLocalFileSystem())
            {
                return PackageFolderFactory.CreatePackageFolderFromPath(source.Source, ignoreFailedSources, reports);
            }
            else
            {
                var httpSource =  new HttpSource(
                    source.Source,
                    source.UserName,
                    source.Password,
                    reports);

                Uri packageBaseAddress;
                if (NuGetv3Feed.DetectNuGetV3(httpSource, noCache, out packageBaseAddress))
                {
                    if (packageBaseAddress == null)
                    {
                        reports.Information.WriteLine(
                            $"Ignoring NuGet v3 feed {source.Source.Yellow().Bold()}, which doesn't provide PackageBaseAddress resource.");
                        return null;
                    }

                    httpSource = new HttpSource(
                        packageBaseAddress.AbsoluteUri,
                        source.UserName,
                        source.Password,
                        reports);

                    return new NuGetv3Feed(
                        httpSource,
                        noCache,
                        reports,
                        ignoreFailedSources);
                }

                return new NuGetv2Feed(
                    httpSource,
                    noCache,
                    reports,
                    ignoreFailedSources);
            }
        }
    }
}