// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.DependencyManagement;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    internal class UninstallCommand
    {
        private readonly IAppCommandsRepository _commandsRepo;
        private readonly Reports _reports;

        public UninstallCommand(IAppCommandsRepository commandsRepo, Reports reports)
        {
            _commandsRepo = commandsRepo;
            _reports = reports;
        }

        public bool NoPurge { get; set; }

        public bool Execute(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                _reports.Error.WriteLine("The name of the command to be uninstalled must be specified.".Red());
                return false;
            }

            if (!_commandsRepo.Commands.Contains(commandName))
            {
                _reports.Error.WriteLine("{0} command is not installed.".Red(), commandName);
                return false;
            }

            _commandsRepo.Remove(commandName);

            if (!NoPurge)
            {
                PurgeOrphanPackages();
            }

            _reports.Information.WriteLine("{0} command has been uninstalled", commandName);

            return true;
        }

        private void PurgeOrphanPackages()
        {
            _reports.Verbose.WriteLine("Removing orphaned packages...");

            // Find the packages still used by the command scripts
            var applicationPackagesStillUsed = _commandsRepo.Commands
                .Select(cmd => _commandsRepo.FindCommandOwner(cmd))
                .Distinct();

            // Get all the installed packages
            var packagesRepo = new PackageRepository(
                _commandsRepo.PackagesRoot,
                caseSensitivePackagesName: false);

            // Key = package<id, version>, Value = bool (true if used, false otherwise)
            var usedPackages = packagesRepo
                    .GetAllPackages()
                    .SelectMany(pack => pack.Value)
                    .ToDictionary(
                        pack => pack, 
                        _ => false);

            var lockFileFormat = new LockFileFormat();
            
            // Mark all the packages still in used by using the dependencies in the lock file
            foreach(var applicationPackage in applicationPackagesStillUsed)
            {
                var appLockFileFullPath = Path.Combine(
                    _commandsRepo.PackagesRoot.Root,
                    _commandsRepo.PathResolver.GetPackageDirectory(applicationPackage.Id, applicationPackage.Version),
                    InstallBuilder.CommandsFolderName,
                    LockFileFormat.LockFileName);

                if (!File.Exists(appLockFileFullPath))
                {
                    _reports.Verbose.WriteLine("Lock file {0} not found. This package will be removed if it is not used by another application", appLockFileFullPath);
                    // The lock file is missing, probably the app is not installed correctly
                    // unless someone else is using it, we'll remove it
                    continue;
                }

                var lockFile = lockFileFormat.Read(appLockFileFullPath);
                foreach(var dependency in lockFile.Libraries)
                {
                    var dependencyPackage = new NuGet.PackageInfo(
                        _commandsRepo.PackagesRoot,
                        dependency.Name,
                        dependency.Version,
                        null,
                        null);

                    if (usedPackages.ContainsKey(dependencyPackage))
                    {
                        // Mark the dependency as used
                        usedPackages[dependencyPackage] = true;
                    }
                }
            }

            // Now it's time to remove those unused packages
            var unusedPackages = usedPackages
                .Where(pack => !pack.Value)
                .Select(pack => pack.Key);

            foreach (var package in unusedPackages)
            {
                packagesRepo.RemovePackage(package);
                _reports.Verbose.WriteLine("Removed orphaned package: {0} {1}", package.Id, package.Version);
            }
        }
    }
}
