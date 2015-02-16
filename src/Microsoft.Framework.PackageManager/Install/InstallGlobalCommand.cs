// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    internal class InstallGlobalCommand
    {
        public const string TargetPackagesFolderName = "packages";

        private readonly IAppCommandsRepository _commandsRepository;

        public InstallGlobalCommand(IApplicationEnvironment env, IAppCommandsRepository commandsRepository)
        {
            RestoreCommand = new RestoreCommand(env);
            _commandsRepository = commandsRepository;
        }

        public RestoreCommand RestoreCommand { get; private set; }

        public Reports Reports
        {
            get { return RestoreCommand.Reports; }
            set { RestoreCommand.Reports = value; }
        }

        public FeedOptions FeedOptions
        {
            get { return RestoreCommand.FeedOptions; }
            set { RestoreCommand.FeedOptions = value; }
        }

        public bool OverwriteCommands { get; set; }

        public async Task<bool> Execute(string packageId, string packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                WriteError("The name of the package to be installed was not specified.");
                return false;
            }

            if (string.IsNullOrEmpty(FeedOptions.TargetPackagesFolder))
            {
                FeedOptions.TargetPackagesFolderOptions.Values.Add(_commandsRepository.PackagesRoot.Root);
            }

            if (packageId != null &&
                packageId.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(packageId))
            {
                var packagePath = Path.GetFullPath(packageId);
                var packageDirectory = Path.GetDirectoryName(packagePath);
                var zipPackage = new NuGet.ZipPackage(packagePath);
                FeedOptions.FallbackSourceOptions.Values.Add(packageDirectory);
                packageId = zipPackage.Id;
                packageVersion = zipPackage.Version.ToString();
            }

            var installPath = Directory.GetParent(FeedOptions.TargetPackagesFolder).FullName;

            RestoreCommand.RestorePackageId = packageId;
            RestoreCommand.RestorePackageVersion = packageVersion;

            return
                await RestoreCommand.ExecuteCommand() &&
                InstallCommands(packageId, RestoreCommand.AppInstallPath);
        }

        private bool InstallCommands(string packageId, string appPath)
        {
            string commandsFolder = Path.Combine(appPath, InstallBuilder.CommandsFolderName);

            if (!Directory.Exists(commandsFolder))
            {
                WriteError("The specified package is not an application. The package was added but no commands were installed.");
                return false;
            }

            var fileFilter = PlatformHelper.IsWindows ? "*.cmd" : "*.sh";
            var allAppCommandsFiles = Directory.EnumerateFiles(commandsFolder, fileFilter);
            var allAppCommands = allAppCommandsFiles
                .Select(cmd => Path.GetFileNameWithoutExtension(cmd))
                .ToList();

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

        private void WriteInfo(string message)
        {
            Reports.Information.WriteLine(message);
        }

        private void WriteError(string message)
        {
            Reports.Error.WriteLine(message.Red());
        }
    }
}