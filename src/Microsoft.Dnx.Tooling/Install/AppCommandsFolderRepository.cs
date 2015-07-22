// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    internal class AppCommandsFolderRepository : IAppCommandsRepository
    {
        private readonly NuGet.IFileSystem _commandsFolder;
        private readonly DefaultPackagePathResolver _pathResolver;

        // Key = command; Value = app name
        private IDictionary<string, NuGet.PackageInfo> _commands = new Dictionary<string, NuGet.PackageInfo>();

        public AppCommandsFolderRepository(string commandsFolder)
        {
            _commandsFolder = new NuGet.PhysicalFileSystem(commandsFolder);
            _pathResolver = new DefaultPackagePathResolver(_commandsFolder);
        }

        public IPackagePathResolver PathResolver
        {
            get
            {
                return _pathResolver;
            }
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
            var commandFileName = commandName +
                (RuntimeEnvironmentHelper.IsWindows ? ".cmd" : string.Empty);

            _commands.Remove(commandName);
            _commandsFolder.DeleteFile(commandFileName);
        }

        public void Load()
        {
            _commands = new Dictionary<string, NuGet.PackageInfo>();

            var pathFilter = RuntimeEnvironmentHelper.IsWindows ? "*.cmd" : "*.*";

            var allCommandFiles = _commandsFolder
                    .GetFiles(".", pathFilter, recursive: false)
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

                // ~dp0\packages\<packageId>\<packageVersion>\app\<commandName>[.cmd]
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
            // TODO: use Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) when it's available on CoreCLR
            var userProfileFolder = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(userProfileFolder))
            {
                userProfileFolder = Environment.GetEnvironmentVariable("HOME");
            }

            if (string.IsNullOrEmpty(userProfileFolder))
            {
                throw new InvalidOperationException("Could not resolve the user profile folder path.");
            }

            var binFolder = Path.Combine(
                userProfileFolder, 
                Runtime.Constants.DefaultLocalRuntimeHomeDir, 
                "bin");
            Directory.CreateDirectory(binFolder);

            return Create(binFolder);
        }
    }
}