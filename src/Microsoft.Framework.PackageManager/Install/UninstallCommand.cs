// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    internal class UninstallCommand
    {
        private readonly IAppCommandsRepository _commandsRepo;
        private readonly IApplicationEnvironment _environment;
        private readonly Reports _reports;

        public UninstallCommand(IApplicationEnvironment applicationEnvironment, IAppCommandsRepository commandsRepo, Reports reports)
        {
            _environment = applicationEnvironment;
            _commandsRepo = commandsRepo;
            _reports = reports;
        }

        public bool NoPurge { get; set; }

        public async Task<bool> Execute(string commandName)
        {
            if (!_commandsRepo.Commands.Contains(commandName))
            {
                _reports.Error.WriteLine("{0} command is not installed.".Red(), commandName);
                return false;
            }

            _commandsRepo.Remove(commandName);

            if (!NoPurge)
            {
                await PurgeOrphanPackages();
            }

            _reports.Information.WriteLine("{0} command has been uninstalled", commandName);

            return true;
        }

        private async Task PurgeOrphanPackages()
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

            // Key = package, Value = bool (true if used, false otherwise)
            var usedPackages = packagesRepo
                    .GetAllPackages()
                    .SelectMany(pack => pack.Value)
                    .ToDictionary(pack => pack, _ => false);

            // Find the dependecies of all packages still in use
            var restoreContext = new RestoreContext()
            {
                FrameworkName = _environment.RuntimeFramework,
                ProjectLibraryProviders = new List<IWalkProvider>(),
                RemoteLibraryProviders = new List<IWalkProvider>(),
                LocalLibraryProviders = new IWalkProvider[] {
                    new LocalWalkProvider(
                        new NuGetDependencyResolver(
                            packagesRepo.RepositoryRoot.Root))
                }
            };
            var operations = new RestoreOperations(_reports.Verbose);
            var dependencyGraphs = await Task.WhenAll(
                applicationPackagesStillUsed.Select(appPackage => 
                    operations.CreateGraphNode(
                        restoreContext,
                        new LibraryRange()
                        {
                            Name = appPackage.Id,
                            VersionRange = new SemanticVersionRange(appPackage.Version)
                        },
                        _ => true)));

            // Mark and sweep the packages still in use
            foreach(var graphRoot in dependencyGraphs)
            {
                MarkAndSweepUsedPackages(graphRoot, usedPackages);
            }

            var unusedPackages = usedPackages
                .Where(pack => !pack.Value)
                .Select(pack => pack.Key);

            foreach (var package in unusedPackages)
            {
                packagesRepo.RemovePackage(package);
                _reports.Verbose.WriteLine("Removed orphaned package: {0} {1}", package.Id, package.Version);
            }
        }

        private void MarkAndSweepUsedPackages(GraphNode root, IDictionary<NuGet.PackageInfo, bool> usedPackages)
        {
            bool alreadyVisited = false;

            var library = root?.Item?.Match?.Library;
            if (library != null)
            {
                var packageInfo = new NuGet.PackageInfo(
                    repositoryRoot: null,
                    packageId: library.Name,
                    version: library.Version,
                    versionDir: null);


                if (usedPackages.ContainsKey(packageInfo))
                {
                    alreadyVisited = usedPackages[packageInfo];

                    if (!alreadyVisited)
                    {
                        // Mark package as used
                        usedPackages[packageInfo] = true;
                    }
                    else
                    {
                        // There is no point to revisit this dependecy since we already did it
                        return;
                    }
                }
            }

            if (!alreadyVisited && root.Dependencies != null)
            {
                foreach(var dependency in root.Dependencies)
                {
                    MarkAndSweepUsedPackages(dependency, usedPackages);
                }
            }
        }
    }
}