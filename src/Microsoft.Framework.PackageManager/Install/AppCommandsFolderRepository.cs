// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    internal class AppCommandsFolderRepository : IAppCommandsRepository
    {
        private readonly NuGet.IFileSystem _commandsFolder;

        // Key = command; Value = app name
        private IDictionary<string, NuGet.PackageInfo> _commands = new Dictionary<string, NuGet.PackageInfo>();

        public AppCommandsFolderRepository(string commandsFolder)
        {
            _commandsFolder = new NuGet.PhysicalFileSystem(commandsFolder);
        }

        public IFileSystem Root
        {
            get
            {
                return _commandsFolder;
            }
        }

        public IFileSystem PackagesRoot
        {
            get
            {
                return _commandsFolder.GetDirectory(InstallGlobalCommand.TargetPackagesFolderName);
            }
        }

        public IEnumerable<string> Commands
        {
            get
            {
                return _commands.Keys;
            }
        }

        public NuGet.PackageInfo FindCommandOwner(string command)
        {
            NuGet.PackageInfo appPackage;
            _commands.TryGetValue(command, out appPackage);
            return appPackage;
        }

        public void Remove(string commandName)
        {
            _commands.Remove(commandName);
            _commandsFolder.DeleteFile(commandName + ".cmd");
        }

        public void Load()
        {
            _commands = new Dictionary<string, NuGet.PackageInfo>();

            var allCommandFiles = _commandsFolder
                .GetFiles(".", "*.cmd", recursive: false)
                .Select(relativePath => _commandsFolder.GetFullPath(relativePath));
            
            foreach (string commandFile in allCommandFiles)
            {
                var lines = File.ReadAllLines(commandFile);

                if (lines.Length != 1)
                {
                    // The run scripts are just one line so this is not an installed app script
                    continue;
                }

                var pathParts = lines[0].Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                // ~dp0\packages\<packageId>\<packageVersion>\app\<commandName>.cmd
                if (pathParts.Length != 6)
                {
                    continue;
                }

                var packageId = pathParts[2];
                SemanticVersion packageVersion;
                if (!SemanticVersion.TryParse(pathParts[3], out packageVersion))
                {
                    continue;
                }

                // version dir = {packagesFolderName}\{packageId}\{version}
                var versionDir = Path.Combine(
                    PackagesRoot.Root,
                    packageId,
                    packageVersion.ToString());

                var appPackage = new NuGet.PackageInfo(
                    PackagesRoot,
                    packageId,
                    packageVersion,
                    versionDir);

                 _commands.Add(
                     Path.GetFileNameWithoutExtension(commandFile),
                     appPackage);
            }
        }

        public static AppCommandsFolderRepository Create(string installPath)
        {
            var binFolder = installPath;
            var installPackagesFolder = Path.Combine(binFolder, InstallGlobalCommand.TargetPackagesFolderName);
            var installGlobalJsonPath = Path.Combine(installPackagesFolder, "global.json");

            if (!File.Exists(installGlobalJsonPath))
            {
                Directory.CreateDirectory(installPackagesFolder);
                File.WriteAllText(installGlobalJsonPath, @"{""packages"":"".""}");
            }

            var repo = new AppCommandsFolderRepository(binFolder);
            repo.Load();

            return repo;
        }

        public static AppCommandsFolderRepository CreateDefault()
        {
            return Create(PathUtilities.DotNetBinFolder);
        }
    }
}