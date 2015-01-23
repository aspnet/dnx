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
    public class InstallGlobalCommand
    {
        private const string TargetPackagesFolderName = "packages";

        public InstallGlobalCommand(IApplicationEnvironment env)
        {
            RestoreCommand = new RestoreCommand(env);
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

        public string PackageId
        {
            get { return RestoreCommand.RestorePackageId; }
            set { RestoreCommand.RestorePackageId = value; }
        }

        public string PackageVersion
        {
            get { return RestoreCommand.RestorePackageVersion; }
            set { RestoreCommand.RestorePackageVersion = value; }
        }

        public bool OverwriteCommands { get; set; }

        public async Task<bool> Install()
        {
            if (string.IsNullOrEmpty(FeedOptions.TargetPackagesFolder))
            {
                var binFolder = PathUtilities.DotNetBinFolder;
                var installPackagesFolder = Path.Combine(binFolder, TargetPackagesFolderName);
                var installGlobalJsonPath = Path.Combine(installPackagesFolder, "global.json");

                if (!File.Exists(installGlobalJsonPath))
                {
                    Directory.CreateDirectory(installPackagesFolder);
                    File.WriteAllText(installGlobalJsonPath, @"{""packages"":"".""}");
                }

                FeedOptions.TargetPackagesFolderOptions.Values.Add(installPackagesFolder);
            }

            if (PackageId != null &&
                PackageId.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(PackageId))
            {
                var packagePath = Path.GetFullPath(PackageId);
                var packageDirectory = Path.GetDirectoryName(packagePath);
                var zipPackage = new NuGet.ZipPackage(packagePath);
                FeedOptions.FallbackSourceOptions.Values.Add(packageDirectory);
                PackageId = zipPackage.Id;
                PackageVersion = zipPackage.Version.ToString();
            }

            //RestoreCommand.RestoreDirectory = FeedOptions.PackageFolder;

            var installPath = Directory.GetParent(FeedOptions.TargetPackagesFolder).FullName;

            return 
                await RestoreCommand.ExecuteCommand() && 
                InstallCommands(installPath, RestoreCommand.AppInstallPath);
        }

        /// <summary>
        /// Installs the commands for a particular app
        /// </summary>
        /// <param name="installPath">Install location</param>
        /// <param name="appPath">Application path</param>
        /// <returns></returns>
        private bool InstallCommands(string installPath, string appPath)
        {
            string commandsFolder = Path.Combine(appPath, InstallBuilder.CommandsFolderName);

            if (!Directory.Exists(commandsFolder))
            {
                WriteError("The specified package is not an application. The package was added but not commands were installed.");
                return false;
            }

            AppCommandsFolderStore commandsStore = new AppCommandsFolderStore(installPath);
            commandsStore.Load();

            var allAppCommandsFiles = Directory.EnumerateFiles(commandsFolder, "*.cmd");
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
                    string commandOwner = commandsStore.FindCommandOwner(appCmd);
                    return !string.IsNullOrEmpty(commandOwner) &&
                        !PackageId.Equals(commandOwner, StringComparison.OrdinalIgnoreCase);
                });
            }

            if (conflictingCommands.Any())
            {
                WriteError(string.Format(
                    "The application commands cannot be installed because the following commands already exist: {0}. No changes were made. Rerun the command with the --overwrite switch to replace the existing commands.",
                    string.Join(", ", conflictingCommands)));

                return false;
            }

            foreach (string commandFileFullPath in allAppCommandsFiles)
            {
                string commandFileName = Path.GetFileName(commandFileFullPath);

                var commandScript = string.Format(
                    "@\"%~dp0{0}\" %*",
                    commandFileFullPath.Substring(installPath.Length));

                File.WriteAllText(
                    Path.Combine(installPath, commandFileName),
                    commandScript);
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