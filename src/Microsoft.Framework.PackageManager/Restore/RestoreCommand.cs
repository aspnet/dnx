// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
#if NET45
using System.IO.Packaging;
#endif
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.PackageManager.Packing;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using Microsoft.Framework.Runtime;
using NuGet;
using NuGet.Common;

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
        }

        public string RestoreDirectory { get; set; }
        public string ConfigFile { get; set; }
        public IEnumerable<string> Sources { get; set; }
        public IEnumerable<string> FallbackSources { get; set; }
        public bool NoCache { get; set; }

        public IApplicationEnvironment ApplicationEnvironment { get; private set; }
        public IMachineWideSettings MachineWideSettings { get; set; }
        public IFileSystem FileSystem { get; set; }
        public IReport Report { get; set; }

        protected internal ISettings Settings { get; set; }
        protected internal IPackageSourceProvider SourceProvider { get; set; }

        public bool ExecuteCommand()
        {
            var sw = new Stopwatch();
            sw.Start();

            var restoreDirectory = RestoreDirectory ?? Directory.GetCurrentDirectory();

            var projectJsonFiles = Directory.GetFiles(restoreDirectory, "project.json", SearchOption.AllDirectories);

            var rootDirectory = ResolveRootDirectory(restoreDirectory);

            ReadSettings(rootDirectory);

            string packagesFolder = Path.Combine(rootDirectory, CommandLineConstants.PackagesDirectoryName);
            var packagesFolderFileSystem = CreateFileSystem(packagesFolder);
            var pathResolver = new DefaultPackagePathResolver(packagesFolderFileSystem, useSideBySidePaths: true);
            var localRepository = new LocalPackageRepository(pathResolver, packagesFolderFileSystem)
            {
                //PackageSaveMode = PackageSaveModes.Nuspec | PackageSaveModes.Nupkg,
            };

            int restoreCount = 0;
            int successCount = 0;
            foreach (var projectJsonPath in projectJsonFiles)
            {
                restoreCount += 1;
                var success = RestoreForProject(localRepository, projectJsonPath, rootDirectory).Result;
                if (success)
                {
                    successCount += 1;
                }
            }

            if (restoreCount > 1)
            {
                Report.WriteLine(string.Format("Total time {0}ms", sw.ElapsedMilliseconds));
            }

            return restoreCount == successCount;
        }

        private async Task<bool> RestoreForProject(LocalPackageRepository localRepository, string projectJsonPath, string rootDirectory)
        {
            var success = true;

            Report.WriteLine(string.Format("Restoring packages for {0}", projectJsonPath.Bold()));

            var sw = new Stopwatch();
            sw.Start();

            Project project;
            if (!Project.TryGetProject(projectJsonPath, out project))
            {
                throw new Exception("TODO: project.json parse error");
            }

            var projectDirectory = project.ProjectDirectory;
            var packagesDirectory = Path.Combine(rootDirectory, CommandLineConstants.PackagesDirectoryName);

            var restoreOperations = new RestoreOperations { Report = Report };
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
                        projectDirectory,
                        packagesDirectory,
                        new EmptyFrameworkResolver())));

            var allSources = SourceProvider.LoadPackageSources();

            var enabledSources = Sources.Any() ?
                Enumerable.Empty<PackageSource>() :
                allSources.Where(s => s.IsEnabled);

            var addedSources = Sources.Concat(FallbackSources).Select(
                value => allSources.FirstOrDefault(source => CorrectName(value, source)) ?? new PackageSource(value));

            var effectiveSources = enabledSources.Concat(addedSources).Distinct().ToList();

            foreach (var source in effectiveSources)
            {
                if (new Uri(source.Source).IsFile)
                {
                    remoteProviders.Add(
                        new RemoteWalkProvider(
                            new PackageFolder(
                                source.Source,
                                Report)));
                }
                else
                {
#if NET45
                    remoteProviders.Add(
                        new RemoteWalkProvider(
                            new PackageFeed(
                                source.Source,
                                source.UserName,
                                source.Password,
                                NoCache,
                                Report)));
#endif
                }
            }

            foreach (var configuration in project.GetTargetFrameworkConfigurations())
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
                    FrameworkName = ApplicationEnvironment.TargetFramework,
                    ProjectLibraryProviders = projectProviders,
                    LocalLibraryProviders = localProviders,
                    RemoteLibraryProviders = remoteProviders,
                });
            }

            var tasks = new List<Task<GraphNode>>();
            foreach (var context in contexts)
            {
                tasks.Add(restoreOperations.CreateGraphNode(context, new Library { Name = project.Name, Version = project.Version }, _ => true));
            }
            var graphs = await Task.WhenAll(tasks);

            Report.WriteLine(string.Format("{0}, {1}ms elapsed", "Resolving complete".Green(), sw.ElapsedMilliseconds));

            var installItems = new List<GraphItem>();
            var missingItems = new List<Library>();
            ForEach(graphs, node =>
            {
                if (node == null || node.Library == null)
                {
                    return;
                }
                if (node.Item == null || node.Item.Match == null)
                {
                    if (node.Library.Version != null && !missingItems.Contains(node.Library))
                    {
                        missingItems.Add(node.Library);
                        Report.WriteLine(string.Format("Unable to locate {0} >= {1}", node.Library.Name.Red().Bold(), node.Library.Version));
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

            foreach (var item in installItems)
            {
                var library = item.Match.Library;

                Report.WriteLine(string.Format("Installing {0} {1}", library.Name.Bold(), library.Version));

                var targetPath = Path.Combine(packagesDirectory, library.Name + "." + library.Version);
                var targetNupkg = Path.Combine(targetPath, library.Name + "." + library.Version + ".nupkg");

                Directory.CreateDirectory(targetPath);
                using (var stream = new FileStream(targetNupkg, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                {
                    await item.Match.Provider.CopyToAsync(item.Match, stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    ExtractPackage(targetPath, stream);
                }
            }

            Report.WriteLine(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));
            return success;
        }

        private static void ExtractPackage(string targetPath, FileStream stream)
        {
#if NET45
            if (PlatformHelper.IsMono)
            {
                using (var archive = Package.Open(stream, FileMode.Open, FileAccess.Read))
                {
                    var packOperations = new PackOperations();
                    packOperations.ExtractNupkg(archive, targetPath);
                }

                return;
            }
#endif

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var packOperations = new PackOperations();
                packOperations.ExtractNupkg(archive, targetPath);
            }
        }

        private bool CorrectName(string value, PackageSource source)
        {
            return source.Name.Equals(value, StringComparison.CurrentCultureIgnoreCase) ||
                source.Source.Equals(value, StringComparison.OrdinalIgnoreCase);
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
                Report.WriteLine(indent + node.Library.Name + "@" + node.Library.Version);
                Display(indent + " ", node.Dependencies);
            }
        }

        public static string ResolveRootDirectory(string projectDir)
        {
            var di = new DirectoryInfo(projectDir);

            while (di.Parent != null)
            {
                if (di.EnumerateFiles("*.global.json").Any() ||
                    di.EnumerateFiles("*.sln").Any() ||
                    di.EnumerateDirectories("packages").Any() ||
                    di.EnumerateDirectories(".git").Any())
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            return Path.GetDirectoryName(projectDir);
        }


        private void ReadSettings(string solutionDirectory)
        {
            // Read the solution-level settings
            var solutionSettingsFile = Path.Combine(
                solutionDirectory,
                NuGetConstants.NuGetSolutionSettingsFolder);
            var fileSystem = CreateFileSystem(solutionSettingsFile);

            if (ConfigFile != null)
            {
                ConfigFile = FileSystem.GetFullPath(ConfigFile);
            }

            Settings = NuGet.Settings.LoadDefaultSettings(
                fileSystem: fileSystem,
                configFileName: ConfigFile,
                machineWideSettings: MachineWideSettings);

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