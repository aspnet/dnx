// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet.Configuration;
using NuGet.Client;
using Settings = NuGet.Configuration.Settings;
// DNU REFACOTRING TODO: for incremental refactoring
using SemanticVersion = NuGet.SemanticVersion;

namespace Microsoft.Framework.PackageManager
{
    internal class InstallGlobalCommand
    {
        public const string TargetPackagesFolderName = "packages";

        private readonly IAppCommandsRepository _commandsRepository;

        public InstallGlobalCommand(IAppCommandsRepository commandsRepository)
        {
            RestoreCommand = new RestoreCommand();
            _commandsRepository = commandsRepository;
        }

        public RestoreCommand RestoreCommand { get; private set; }

        public ILogger Logger
        {
            get { return RestoreCommand.Logger; }
            set { RestoreCommand.Logger = value; }
        }

        public FeedOptions FeedOptions
        {
            get { return RestoreCommand.FeedOptions; }
            set { RestoreCommand.FeedOptions = value; }
        }

        public bool OverwriteCommands { get; set; }

        public async Task<bool> Execute(string packageId, string packageVersion)
        {
            // 0. Resolve the actual package id and version
            var packageIdAndVersion = await ResolvePackageIdAndVersion(packageId, packageVersion);

            if (packageIdAndVersion == null)
            {
                WriteError("The name of the package to be installed was not specified.");
                return false;
            }

            packageId = packageIdAndVersion.Item1;
            packageVersion = packageIdAndVersion.Item2;

            WriteVerbose("Resolved package id: {0}", packageId);
            WriteVerbose("Resolved package version: {0}", packageVersion);

            // 1. Get the package without dependencies. We cannot resolve them now because
            // we don't know the target frameworks that the package supports

            if (string.IsNullOrEmpty(FeedOptions.TargetPackagesFolder))
            {
                FeedOptions.TargetPackagesFolderOptions.Values.Add(_commandsRepository.PackagesRoot.Root);
            }

            var temporaryProjectFileFullPath = CreateTemporaryProject(FeedOptions.TargetPackagesFolder, packageId, packageVersion);

            try
            {
                RestoreCommand.RestoreDirectory = temporaryProjectFileFullPath;
                if (!await RestoreCommand.ExecuteCommand())
                {
                    return false;
                }
            }
            finally
            {
                var folderToDelete = Path.GetDirectoryName(temporaryProjectFileFullPath);
                FileOperationUtils.DeleteFolder(folderToDelete);
                Directory.Delete(folderToDelete);
            }

            var packageFullPath = Path.Combine(
                _commandsRepository.PackagesRoot.Root,
                _commandsRepository.PathResolver.GetPackageDirectory(packageId, new SemanticVersion(packageVersion)));

            if (!ValidateApplicationPackage(packageFullPath))
            {
                return false;
            }

            var packageAppFolder = Path.Combine(
                packageFullPath,
                InstallBuilder.CommandsFolderName);

            // 2. Now, that we have a valid app package, we can resolve its dependecies
            RestoreCommand.RestoreDirectory = packageAppFolder;
            if (!await RestoreCommand.ExecuteCommand())
            {
                return false;
            }

            // 3. Dependencies are in place, now let's install the commands
            if (!InstallCommands(packageId, packageFullPath))
            {
                return false;
            }

            return true;
        }

        private async Task<Tuple<string, string>> ResolvePackageIdAndVersion(string packageId, string packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return null;
            }

            // For nupkgs, get the id and version from the package
            if (packageId.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(packageId))
                {
                    WriteError(string.Format("Could not find the file {0}.", packageId));
                    return null;
                }

                var packagePath = Path.GetFullPath(packageId);
                var packageDirectory = Path.GetDirectoryName(packagePath);
                var zipPackage = new NuGet.ZipPackage(packagePath);
                FeedOptions.FallbackSourceOptions.Values.Add(packageDirectory);

                return new Tuple<string, string>(
                    zipPackage.Id,
                    zipPackage.Version.ToString());
            }

            // If the version is missing, try to find the latest version
            if (string.IsNullOrEmpty(packageVersion))
            {
                var rootDirectory = ProjectResolver.ResolveRootDirectory(_commandsRepository.Root.Root);
                var settings = Settings.LoadDefaultSettings(
                    rootDirectory,
                    configFileName: null,
                    machineWideSettings: RestoreCommand.MachineWideSettings);

                var sourceProvier = PackageSourceBuilder.CreateSourceProvider(settings);

                var packageFeeds = new List<IPackageFeed>();

                var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(
                    sourceProvier,
                    FeedOptions.Sources,
                    FeedOptions.FallbackSources);

                foreach (var source in effectiveSources)
                {
                    var feed = PackageSourceUtils.CreatePackageFeed(
                        source,
                        FeedOptions.NoCache,
                        FeedOptions.IgnoreFailedSources,
                        Logger);
                    if (feed != null)
                    {
                        packageFeeds.Add(feed);
                    }
                }

                var package = await PackageSourceUtils.FindLatestPackage(packageFeeds, packageId);

                if (package == null)
                {
                    Logger.WriteError("Unable to locate the package {0}".Red(), packageId);
                    return null;
                }

                return new Tuple<string, string>(
                    packageId,
                    package.Version.ToString());
            }

            // Otherwise, just assume that what you got is correct
            return new Tuple<string, string>(packageId, packageVersion);
        }

        // Creates a temporary project with the specified package as dependency
        private string CreateTemporaryProject(string installFolder, string packageName, string packageVersion)
        {
            var tempFolderName = Guid.NewGuid().ToString("N");
            var tempFolderFullPath = Path.Combine(installFolder, tempFolderName);

            // Delete if exists already
            FileOperationUtils.DeleteFolder(tempFolderFullPath);
            Directory.CreateDirectory(tempFolderFullPath);

            WriteVerbose("Temporary folder name: {0}", tempFolderFullPath);
            var projectFileFullPath = Path.Combine(tempFolderFullPath, "project.json");

            File.WriteAllText(
                projectFileFullPath,
                string.Format(
        @"{{
    ""dependencies"":{{
        ""{0}"":""{1}""
    }}
}}", packageName, packageVersion));

            return projectFileFullPath;
        }

        private bool ValidateApplicationPackage(string appFolderFullPath)
        {
            string commandsFolder = Path.Combine(appFolderFullPath, InstallBuilder.CommandsFolderName);

            if (!Directory.Exists(commandsFolder))
            {
                WriteError("The specified package is not an application. The package was added but no commands were installed.");
                return false;
            }

            var blockedCommands = _commandsRepository.Commands.Where(cmd =>
                !CommandNameValidator.IsCommandNameValid(cmd) ||
                CommandNameValidator.ShouldNameBeSkipped(cmd));
            if (blockedCommands.Any())
            {
                WriteError(string.Format(
                    "The application cannot be installed because the following command names are not allowed: {0}.",
                    string.Join(", ", blockedCommands)));

                return false;
            }

            return true;
        }

        private bool InstallCommands(string packageId, string appPath)
        {
            string commandsFolder = Path.Combine(appPath, InstallBuilder.CommandsFolderName);

            IEnumerable<string> allAppCommandsFiles;

            if (PlatformHelper.IsWindows)
            {
                allAppCommandsFiles = Directory.EnumerateFiles(commandsFolder, "*.cmd");
            }
            else
            {
                // We have cmd files and project.*.json files in the same folder
                allAppCommandsFiles = Directory.EnumerateFiles(commandsFolder, "*.*")
                    .Where(cmd => !cmd.EndsWith(".cmd") && !cmd.EndsWith(".json"))
                    .ToList();
            }

            var allAppCommands = allAppCommandsFiles
                .Select(cmd => Path.GetFileNameWithoutExtension(cmd))
                .ToList();


            IEnumerable<string> conflictingCommands;
            if (OverwriteCommands)
            {
                conflictingCommands = new string[0];
            }
            else
            {
                // Conflicting commands are only the commands not owned by this application
                conflictingCommands = allAppCommands.Where(appCmd =>
                {
                    var commandOwner = _commandsRepository.FindCommandOwner(appCmd);
                    return commandOwner != null &&
                        !packageId.Equals(commandOwner.Id, StringComparison.OrdinalIgnoreCase);
                });
            }

            if (conflictingCommands.Any())
            {
                WriteError(string.Format(
                    "The application commands cannot be installed because the following commands already exist: {0}. No changes were made. Rerun the command with the --overwrite switch to replace the existing commands.",
                    string.Join(", ", conflictingCommands)));

                return false;
            }

            var installPath = Path.GetFullPath(_commandsRepository.Root.Root);
            foreach (string commandFileFullPath in allAppCommandsFiles)
            {
                string commandFileName =
                    PlatformHelper.IsWindows ?
                    Path.GetFileName(commandFileFullPath) :
                    Path.GetFileNameWithoutExtension(commandFileFullPath);

                string commandScript;

                if (PlatformHelper.IsWindows)
                {
                    commandScript = string.Format(
                        "@\"%~dp0{0}\" %*",
                        commandFileFullPath.Substring(installPath.Length));
                }
                else
                {
                    commandScript = string.Format(
                        "\"$(dirname $0){0}\" $@",
                        commandFileFullPath.Substring(installPath.Length));
                }

                string scriptFilePath = Path.Combine(installPath, commandFileName);
                File.WriteAllText(scriptFilePath, commandScript);

                if (!PlatformHelper.IsWindows)
                {
                    FileOperationUtils.MarkExecutable(commandFileFullPath);
                    FileOperationUtils.MarkExecutable(scriptFilePath);
                }
            }

            var installedCommands = allAppCommandsFiles.Select(cmd => Path.GetFileNameWithoutExtension(cmd));

            WriteInfo("The following commands were installed: " + string.Join(", ", installedCommands));
            return true;
        }

        private void WriteVerbose(string message, params string[] args)
        {
            Logger.WriteVerbose(message, args);
        }

        private void WriteInfo(string message)
        {
            Logger.WriteInformation(message);
        }

        private void WriteError(string message)
        {
            Logger.WriteError(message.Red());
        }
    }
}
