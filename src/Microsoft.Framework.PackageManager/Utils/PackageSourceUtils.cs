// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;

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

        public static IPackageFeed CreatePackageFeed(PackageSource source, bool noCache, bool ignoreFailedSources,
            Reports reports)
        {
            if (new Uri(source.Source).IsFile)
            {
                return PackageFolderFactory.CreatePackageFolderFromPath(source.Source, ignoreFailedSources, reports);
            }
            else
            {
                return new NuGetv2Feed(
                    source.Source,
                    source.UserName,
                    source.Password,
                    noCache,
                    reports,
                    ignoreFailedSources);
            }
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
            SemanticVersionRange idealVersion)
        {
            var tasks = new List<Task<IEnumerable<PackageInfo>>>();

            foreach (var feed in packageFeeds)
            {
                tasks.Add(feed.FindPackagesByIdAsync(packageName));
            }

            var results = (await Task.WhenAll(tasks)).SelectMany(x => x);
            PackageInfo bestResult = null;

            foreach (var result in results)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestResult?.Version,
                    considering: result.Version,
                    ideal: idealVersion))
                {
                    bestResult = result;
                }
            }

            return bestResult;
        }

        private static bool CorrectName(string value, PackageSource source)
        {
            return source.Name.Equals(value, StringComparison.CurrentCultureIgnoreCase) ||
                source.Source.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
    }
}