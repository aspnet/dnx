// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Versioning;
using NuGet.Configuration;

namespace Microsoft.Framework.PackageManager
{
    public static class PackageSourceUtils
    {
        public static List<PackageSource> GetEffectivePackageSources(IPackageSourceProvider sourceProvider,
            IEnumerable<string> sources, IEnumerable<string> fallbackSources)
        {
            var allSources = sourceProvider.LoadPackageSources();
            var enabledSources = sources.Any() ?
                Enumerable.Empty<PackageSource>() :
                allSources.Where(s => s.IsEnabled);

            var addedSources = sources.Concat(fallbackSources).Select(
                value => allSources.FirstOrDefault(source => CorrectName(value, source)) ?? new PackageSource(value));

            return enabledSources.Concat(addedSources).Distinct().ToList();
        }

        public static IPackageFeed CreatePackageFeed(PackageSource source, bool noCache, bool ignoreFailedSources, ILogger logger)
        {
            return PackageFeedFactory.CreateFeed(source.Source, source.UserName, source.Password, noCache, ignoreFailedSources, logger);
        }

        public static async Task<PackageInfo> FindLatestPackage(IEnumerable<IPackageFeed> packageFeeds, string packageName)
        {
            var tasks = new List<Task<IEnumerable<PackageInfo>>>();

            foreach (var feed in packageFeeds)
            {
                tasks.Add(feed.FindPackagesByIdAsync(packageName));
            }

            var results = (await Task.WhenAll(tasks)).SelectMany(x => x);

            return GetMaxVersion(results);
        }

        private static PackageInfo GetMaxVersion(IEnumerable<PackageInfo> packageInfos)
        {
            var max = packageInfos.FirstOrDefault();

            foreach (var packageInfo in packageInfos)
            {
                max = max.Version > packageInfo.Version ? max : packageInfo;
            }

            return max;
        }

        public static async Task<PackageInfo> FindBestMatchPackage(
            IEnumerable<IPackageFeed> packageFeeds, 
            string packageName,
            NuGetVersion idealVersion)
        {
            var tasks = new List<Task<IEnumerable<PackageInfo>>>();

            foreach (var feed in packageFeeds)
            {
                tasks.Add(feed.FindPackagesByIdAsync(packageName));
            }

            var results = (await Task.WhenAll(tasks)).SelectMany(x => x);

            return results.FindBestMatch(new VersionRange(idealVersion), p => p.Version);
        }

        private static bool CorrectName(string value, PackageSource source)
        {
            return source.Name.Equals(value, StringComparison.CurrentCultureIgnoreCase) ||
                source.Source.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
    }
}