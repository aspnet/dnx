// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet.Configuration;
using NuGet.Client;
using NuGet.Versioning;

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

        public ILogger Logger { get; set; }

        public async Task<bool> ExecuteCommand()
        {
            if (string.IsNullOrEmpty(_addCommand.Name))
            {
                Logger.WriteError("Name of dependency to install is required.".Red());
                return false;
            }

            NuGetVersion version = null;
            if (!string.IsNullOrEmpty(_addCommand.Version))
            {
                version = NuGetVersion.Parse(_addCommand.Version);
            }

            // Create source provider from solution settings
            _addCommand.ProjectDir = _addCommand.ProjectDir ?? Directory.GetCurrentDirectory();

            var rootDir = ProjectResolver.ResolveRootDirectory(_addCommand.ProjectDir);
            var settings = Settings.LoadDefaultSettings(rootDir,
                configFileName: null,
                machineWideSettings: _restoreCommand.MachineWideSettings);
            var sourceProvider = PackageSourceBuilder.CreateSourceProvider(settings);

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(sourceProvider,
                _restoreCommand.FeedOptions.Sources, _restoreCommand.FeedOptions.FallbackSources);

            var packageFeeds = new List<IPackageFeed>();

            foreach (var source in effectiveSources)
            {
                var feed = PackageSourceUtils.CreatePackageFeed(
                    source,
                    _restoreCommand.FeedOptions.NoCache,
                    _restoreCommand.FeedOptions.IgnoreFailedSources,
                    Logger);
                if (feed != null)
                {
                    packageFeeds.Add(feed);
                }
            }

            PackageInfo result = null;

            if (version == null)
            {
                result = await PackageSourceUtils.FindLatestPackage(packageFeeds, _addCommand.Name);
            }
            else
            {
                result = await PackageSourceUtils.FindBestMatchPackage(packageFeeds, _addCommand.Name, new NuGetVersion(version));
            }

            if (result == null)
            {
                Logger.WriteError("Unable to locate {0} >= {1}",
                    _addCommand.Name.Red().Bold(), _addCommand.Version);
                return false;
            }

            if (string.IsNullOrEmpty(_addCommand.Version))
            {
                _addCommand.Version = result.Version.ToString();
            }

            return _addCommand.ExecuteCommand() && (await _restoreCommand.ExecuteCommand());
        }
    }
}
