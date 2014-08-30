// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class InstallCommand
    {
        private readonly AddCommand _addCommand;
        private readonly RestoreCommand _restoreCommand;

        public InstallCommand(AddCommand addCmd, RestoreCommand restoreCmd)
        {
            _addCommand = addCmd;
            _restoreCommand = restoreCmd;
        }

        public Reports Reports { get; set; }

        public async Task<bool> ExecuteCommand()
        {
            if (string.IsNullOrEmpty(_addCommand.Name))
            {
                Reports.Information.WriteLine("Name of dependency to install is required.");
                return false;
            }

            SemanticVersion version = null;
            if (!string.IsNullOrEmpty(_addCommand.Version))
            {
                version = SemanticVersion.Parse(_addCommand.Version);
            }

            // Create source provider from solution settings
            _addCommand.ProjectDir = _addCommand.ProjectDir ?? Directory.GetCurrentDirectory();
            var rootDir = ProjectResolver.ResolveRootDirectory(_addCommand.ProjectDir);
            var fileSystem = new PhysicalFileSystem(Directory.GetCurrentDirectory());
            var settings = SettingsUtils.ReadSettings(solutionDir: rootDir,
                nugetConfigFile: null,
                fileSystem: fileSystem,
                machineWideSettings: new CommandLineMachineWideSettings());
            var sourceProvider = PackageSourceBuilder.CreateSourceProvider(settings);

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(sourceProvider,
                _restoreCommand.Sources, _restoreCommand.FallbackSources);

            var packageFeeds = new List<IPackageFeed>();
            foreach (var source in effectiveSources)
            {
                if (new Uri(source.Source).IsFile)
                {
                    packageFeeds.Add(new PackageFolder(source.Source, Reports.Quiet));
                }
                else
                {
                    packageFeeds.Add(new PackageFeed(
                        source.Source, source.UserName, source.Password, _restoreCommand.NoCache, Reports.Quiet));
                }
            }

            PackageInfo result = null;
            if (version == null)
            {
                result = await FindLatestVersion(packageFeeds, _addCommand.Name);
            }
            else
            {
                result = await FindBestMatch(packageFeeds, _addCommand.Name, version);
            }

            if (result == null)
            {
                Reports.Information.WriteLine("Unable to locate {0} >= {1}", _addCommand.Name, _addCommand.Version);
                return false;
            }

            if (string.IsNullOrEmpty(_addCommand.Version))
            {
                _addCommand.Version = result.Version.ToString();
            }

            return _addCommand.ExecuteCommand() && (await _restoreCommand.ExecuteCommand());
        }

        private static async Task<PackageInfo> FindLatestVersion(IEnumerable<IPackageFeed> packageFeeds, string packageName)
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

        private static async Task<PackageInfo> FindBestMatch(IEnumerable<IPackageFeed> packageFeeds, string packageName,
            SemanticVersion idealVersion)
        {
            var tasks = new List<Task<IEnumerable<PackageInfo>>>();
            foreach (var feed in packageFeeds)
            {
                tasks.Add(feed.FindPackagesByIdAsync(packageName));
            }
            var results = (await Task.WhenAll(tasks)).SelectMany(x => x);

            var bestResult = results.FirstOrDefault();
            foreach (var result in results)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestResult.Version,
                    considering: result.Version,
                    ideal: idealVersion))
                {
                    bestResult = result;
                }
            }
            return bestResult;
        }
    }
}
