// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.PackageManager.Packing;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Loader.NuGet;
using NuGet;
using NuGet.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreCommand
    {
        public RestoreCommand(IApplicationEnvironment env)
        {
            ApplicationEnvironment = env;
            FileSystem = new PhysicalFileSystem(Environment.CurrentDirectory);
            MachineWideSettings = new CommandLineMachineWideSettings();
            Sources = Enumerable.Empty<string>();
            FallbackSources = Enumerable.Empty<string>();
        }

        public string RestoreDirectory { get; set; }
        public string ConfigFile { get; set; }
        public IEnumerable<string> Sources { get; set; }
        public IEnumerable<string> FallbackSources { get; set; }

        public IApplicationEnvironment ApplicationEnvironment { get; private set; }
        public IMachineWideSettings MachineWideSettings { get; set; }
        public IFileSystem FileSystem { get; set; }
        public IReport Report { get; set; }

        protected internal ISettings Settings { get; set; }
        protected internal IPackageSourceProvider SourceProvider { get; set; }

        public void ExecuteCommand()
        {
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

            foreach (var projectJsonPath in projectJsonFiles)
            {
                RestoreForProject(localRepository, projectJsonPath, rootDirectory).Wait();
            }
        }

        private async Task RestoreForProject(LocalPackageRepository localRepository, string projectJsonPath, string rootDirectory)
        {
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
                        new FrameworkReferenceResolver())));

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
                    remoteProviders.Add(
                        new RemoteWalkProvider(
                            new PackageFeed(
                                source.Source,
                                source.UserName,
                                source.Password,
                                Report)));
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

            Report.WriteLine(string.Format("Resolving complete, {0}ms elapsed".Green(), sw.ElapsedMilliseconds));

            var installItems = new List<GraphItem>();
            ForEach(graphs, node =>
            {
                if (node == null || node.Item == null || node.Item.Match == null)
                {
                    return;
                }
                var isRemote = remoteProviders.Contains(node.Item.Match.Provider);
                var isAdded = installItems.Any(item => item.Match.Library == node.Item.Match.Library);
                if (isRemote && !isAdded)
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
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        var packOperations = new PackOperations();
                        packOperations.ExtractNupkg(archive, targetPath);
                    }
                }
            }

            Report.WriteLine(string.Format("Restore complete, {0}ms elapsed".Green().Bold(), sw.ElapsedMilliseconds));
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