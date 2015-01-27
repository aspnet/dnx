// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Framework.PackageManager.Bundle;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreCommand
    {
        public RestoreCommand(IApplicationEnvironment env)
        {
            ApplicationEnvironment = env;
            FileSystem = new PhysicalFileSystem(Directory.GetCurrentDirectory());
            MachineWideSettings = new CommandLineMachineWideSettings();
            Sources = Enumerable.Empty<string>();
            FallbackSources = Enumerable.Empty<string>();
            ScriptExecutor = new ScriptExecutor();
        }

        public string RestoreDirectory { get; set; }
        public string NuGetConfigFile { get; set; }
        public IEnumerable<string> Sources { get; set; }
        public IEnumerable<string> FallbackSources { get; set; }
        public bool NoCache { get; set; }
        public string PackageFolder { get; set; }
        public string GlobalJsonFile { get; set; }
        public bool IgnoreFailedSources { get; set; }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public IApplicationEnvironment ApplicationEnvironment { get; private set; }
        public IMachineWideSettings MachineWideSettings { get; set; }
        public IFileSystem FileSystem { get; set; }
        public Reports Reports { get; set; }

        protected internal ISettings Settings { get; set; }
        protected internal IPackageSourceProvider SourceProvider { get; set; }

        public async Task<bool> ExecuteCommand()
        {
            try
            {
                var sw = Stopwatch.StartNew();

                // If the root argument is a project.json file
                if (string.Equals(
                    Runtime.Project.ProjectFileName,
                    Path.GetFileName(RestoreDirectory),
                    StringComparison.OrdinalIgnoreCase))
                {
                    RestoreDirectory = Path.GetDirectoryName(Path.GetFullPath(RestoreDirectory));
                }
                // If the root argument is a global.json file
                else if (string.Equals(
                    GlobalSettings.GlobalFileName,
                    Path.GetFileName(RestoreDirectory),
                    StringComparison.OrdinalIgnoreCase))
                {
                    GlobalJsonFile = RestoreDirectory;
                    RestoreDirectory = Path.GetDirectoryName(Path.GetFullPath(RestoreDirectory));
                }
                else if (!Directory.Exists(RestoreDirectory) && !string.IsNullOrEmpty(RestoreDirectory))
                {
                    throw new InvalidOperationException("The given root is invalid.");
                }

                var restoreDirectory = RestoreDirectory ?? Directory.GetCurrentDirectory();

                var rootDirectory = ProjectResolver.ResolveRootDirectory(restoreDirectory);
                ReadSettings(rootDirectory);

                string packagesDirectory = PackageFolder;

                if (string.IsNullOrEmpty(PackageFolder))
                {
                    packagesDirectory = NuGetDependencyResolver.ResolveRepositoryPath(rootDirectory);
                }

                var packagesFolderFileSystem = CreateFileSystem(packagesDirectory);
                var pathResolver = new DefaultPackagePathResolver(packagesFolderFileSystem, useSideBySidePaths: true);

                int restoreCount = 0;
                int successCount = 0;

                if (string.IsNullOrEmpty(GlobalJsonFile))
                {
                    var projectJsonFiles = Directory.GetFiles(restoreDirectory, "project.json", SearchOption.AllDirectories);
                    foreach (var projectJsonPath in projectJsonFiles)
                    {
                        restoreCount += 1;
                        var success = await RestoreForProject(projectJsonPath, rootDirectory, packagesDirectory);
                        if (success)
                        {
                            successCount += 1;
                        }
                    }
                }
                else
                {
                    restoreCount = 1;
                    var success = await RestoreFromGlobalJson(rootDirectory, packagesDirectory);
                    if (success)
                    {
                        successCount = 1;
                    }
                }

                if (restoreCount > 1)
                {
                    Reports.Information.WriteLine(string.Format("Total time {0}ms", sw.ElapsedMilliseconds));
                }

                return restoreCount == successCount;
            }
            catch (Exception ex)
            {
                Reports.Information.WriteLine("----------");
                Reports.Information.WriteLine(ex.ToString());
                Reports.Information.WriteLine("----------");
                Reports.Information.WriteLine("Restore failed");
                Reports.Information.WriteLine(ex.Message);
                return false;
            }
        }

        private async Task<bool> RestoreForProject(string projectJsonPath, string rootDirectory, string packagesDirectory)
        {
            var success = true;

            Reports.Information.WriteLine(string.Format("Restoring packages for {0}", projectJsonPath.Bold()));

            var sw = new Stopwatch();
            sw.Start();

            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(projectJsonPath, out project))
            {
                throw new Exception("TODO: project.json parse error");
            }

            Func<string, string> getVariable = key =>
            {
                return null;
            };

            if (!ScriptExecutor.Execute(project, "prerestore", getVariable))
            {
                Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            var projectDirectory = project.ProjectDirectory;
            var restoreOperations = new RestoreOperations(Reports.Verbose);
            var projectProviders = new List<IWalkProvider>();
            var localProviders = new List<IWalkProvider>();
            var remoteProviders = new List<IWalkProvider>();
            var contexts = new List<RestoreContext>();

            projectProviders.Add(
                new LocalWalkProvider(
                    new ProjectReferenceDependencyProvider(
                        new ProjectResolver(
                            projectDirectory,
                            rootDirectory))));

            localProviders.Add(
                new LocalWalkProvider(
                    new NuGetDependencyResolver(
                        packagesDirectory)));

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(SourceProvider,
                Sources, FallbackSources);

            AddRemoteProvidersFromSources(remoteProviders, effectiveSources);

            foreach (var configuration in project.GetTargetFrameworks())
            {
                var context = new RestoreContext
                {
                    FrameworkName = configuration.FrameworkName,
                    ProjectLibraryProviders = projectProviders,
                    LocalLibraryProviders = localProviders,
                    RemoteLibraryProviders = remoteProviders,
                };
                contexts.Add(context);
            }

            if (!contexts.Any())
            {
                contexts.Add(new RestoreContext
                {
                    FrameworkName = ApplicationEnvironment.RuntimeFramework,
                    ProjectLibraryProviders = projectProviders,
                    LocalLibraryProviders = localProviders,
                    RemoteLibraryProviders = remoteProviders,
                });
            }

            var tasks = new List<Task<GraphNode>>();

            foreach (var context in contexts)
            {
                var projectLibrary = new LibraryRange
                {
                    Name = project.Name,
                    VersionRange = new SemanticVersionRange(project.Version)
                };

                tasks.Add(restoreOperations.CreateGraphNode(context, projectLibrary, _ => true));
            }
            var graphs = await Task.WhenAll(tasks);

            Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Resolving complete".Green(), sw.ElapsedMilliseconds));

            var installItems = new List<GraphItem>();
            var missingItems = new HashSet<LibraryRange>();

            ForEach(graphs, node =>
            {
                if (node == null || node.LibraryRange == null)
                {
                    return;
                }

                if (node.Item == null || node.Item.Match == null)
                {
                    if (!node.LibraryRange.IsGacOrFrameworkReference &&
                         node.LibraryRange.VersionRange != null &&
                         missingItems.Add(node.LibraryRange))
                    {
                        Reports.Error.WriteLine(string.Format("Unable to locate {0} {1}", node.LibraryRange.Name.Red().Bold(), node.LibraryRange.VersionRange));
                        success = false;
                    }

                    return;
                }

                // "kpm restore" is case-sensitive
                if (!string.Equals(node.Item.Match.Library.Name, node.LibraryRange.Name, StringComparison.Ordinal))
                {
                    if (missingItems.Add(node.LibraryRange))
                    {
                        Reports.Error.WriteLine("Unable to locate {0} {1}. Do you mean {2}?",
                            node.LibraryRange.Name.Red().Bold(), node.LibraryRange.VersionRange, node.Item.Match.Library.Name.Bold());

                        success = false;
                    }

                    return;
                }

                var isRemote = remoteProviders.Contains(node.Item.Match.Provider);
                var isAdded = installItems.Any(item => item.Match.Library == node.Item.Match.Library);

                if (!isAdded && isRemote)
                {
                    installItems.Add(node.Item);
                }
            });

            await InstallPackages(installItems, packagesDirectory, packageFilter: (library, nupkgSHA) => true);

            if (!ScriptExecutor.Execute(project, "postrestore", getVariable))
            {
                Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ScriptExecutor.Execute(project, "prepare", getVariable))
            {
                Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

            return success;
        }

        private async Task<bool> RestoreFromGlobalJson(string rootDirectory, string packagesDirectory)
        {
            var success = true;

            Reports.Information.WriteLine(string.Format("Restoring packages for {0}", Path.GetFullPath(GlobalJsonFile).Bold()));

            var sw = new Stopwatch();
            sw.Start();

            var restoreOperations = new RestoreOperations(Reports.Information);
            var localProviders = new List<IWalkProvider>();
            var remoteProviders = new List<IWalkProvider>();

            localProviders.Add(
                new LocalWalkProvider(
                    new NuGetDependencyResolver(
                        packagesDirectory)));

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(SourceProvider,
                Sources, FallbackSources);

            AddRemoteProvidersFromSources(remoteProviders, effectiveSources);

            var context = new RestoreContext
            {
                FrameworkName = ApplicationEnvironment.RuntimeFramework,
                ProjectLibraryProviders = new List<IWalkProvider>(),
                LocalLibraryProviders = localProviders,
                RemoteLibraryProviders = remoteProviders,
            };

            GlobalSettings globalSettings;
            GlobalSettings.TryGetGlobalSettings(GlobalJsonFile, out globalSettings);

            var libsToRestore = globalSettings.PackageHashes.Keys.ToList();

            var tasks = new List<Task<GraphItem>>();

            foreach (var library in libsToRestore)
            {
                tasks.Add(restoreOperations.FindLibraryCached(context, library));
            }

            var resolvedItems = await Task.WhenAll(tasks);

            Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Resolving complete".Green(),
                sw.ElapsedMilliseconds));

            var installItems = new List<GraphItem>();
            var missingItems = new List<Library>();

            for (int i = 0; i < resolvedItems.Length; i++)
            {
                var item = resolvedItems[i];
                var library = libsToRestore[i];

                if (item == null || 
                    item.Match == null || 
                    item.Match.Library.Version != library.Version)
                {
                    missingItems.Add(library);

                    Reports.Error.WriteLine(string.Format("Unable to locate {0} {1}",
                        library.Name.Red().Bold(), library.Version));

                    success = false;
                    continue;
                }

                var isRemote = remoteProviders.Contains(item.Match.Provider);
                var isAdded = installItems.Any(x => x.Match.Library == item.Match.Library);

                if (!isAdded && isRemote)
                {
                    installItems.Add(item);
                }
            }

            await InstallPackages(installItems, packagesDirectory, packageFilter: (library, nupkgSHA) =>
            {
                string expectedSHA = globalSettings.PackageHashes[library];

                if (!string.Equals(expectedSHA, nupkgSHA, StringComparison.Ordinal))
                {
                    Reports.Error.WriteLine(
                        string.Format("SHA of downloaded package {0} doesn't match expected value.".Red().Bold(),
                        library.ToString()));

                    success = false;
                    return false;
                }

                return true;
            });

            Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

            return success;
        }

        private async Task InstallPackages(List<GraphItem> installItems, string packagesDirectory,
            Func<Library, string, bool> packageFilter)
        {
            using (var sha512 = SHA512.Create())
            {
                foreach (var item in installItems)
                {
                    var library = item.Match.Library;

                    var memStream = new MemoryStream();
                    await item.Match.Provider.CopyToAsync(item.Match, memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
                    var nupkgSHA = Convert.ToBase64String(sha512.ComputeHash(memStream));

                    bool shouldInstall = packageFilter(library, nupkgSHA);
                    if (!shouldInstall)
                    {
                        continue;
                    }

                    Reports.Information.WriteLine("Installing {0} {1}", library.Name.Bold(), library.Version);
                    memStream.Seek(0, SeekOrigin.Begin);
                    await NuGetPackageUtils.InstallFromStream(memStream, library, packagesDirectory, sha512);
                }
            }
        }

        private void AddRemoteProvidersFromSources(List<IWalkProvider> remoteProviders, List<PackageSource> effectiveSources)
        {
            foreach (var source in effectiveSources)
            {
                var feed = PackageSourceUtils.CreatePackageFeed(source, NoCache, IgnoreFailedSources, Reports);
                if (feed != null)
                {
                    remoteProviders.Add(new RemoteWalkProvider(feed));
                }
            }
        }

        private static void ExtractPackage(string targetPath, FileStream stream)
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var packOperations = new BundleOperations();
                packOperations.ExtractNupkg(archive, targetPath);
            }
        }

        void ForEach(IEnumerable<GraphNode> nodes, Action<GraphNode> callback)
        {
            foreach (var node in nodes)
            {
                callback(node);
                ForEach(node.Dependencies, callback);
            }
        }

        void Display(string indent, IEnumerable<GraphNode> graphs)
        {
            foreach (var node in graphs)
            {
                Reports.Information.WriteLine(indent + node.Item.Match.Library.Name + "@" + node.Item.Match.Library.Version);
                Display(indent + " ", node.Dependencies);
            }
        }


        private void ReadSettings(string solutionDirectory)
        {
            Settings = SettingsUtils.ReadSettings(solutionDirectory, NuGetConfigFile, FileSystem, MachineWideSettings);

            // Recreate the source provider and credential provider
            SourceProvider = PackageSourceBuilder.CreateSourceProvider(Settings);
            //HttpClient.DefaultCredentialProvider = new SettingsCredentialProvider(new ConsoleCredentialProvider(Console), SourceProvider, Console);

        }

        private IFileSystem CreateFileSystem(string path)
        {
            path = FileSystem.GetFullPath(path);
            return new PhysicalFileSystem(path);
        }
    }
}