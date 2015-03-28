// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.PackageManager.Bundle;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using Microsoft.Framework.PackageManager.Utils;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.DependencyManagement;
using NuGet;
using TempRepack.Engine.Model;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreCommand
    {
        public RestoreCommand(IApplicationEnvironment env)
        {
            ApplicationEnvironment = env;
            FileSystem = new PhysicalFileSystem(Directory.GetCurrentDirectory());
            MachineWideSettings = new CommandLineMachineWideSettings();
            ScriptExecutor = new ScriptExecutor();
            ErrorMessages = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        }

        public FeedOptions FeedOptions { get; set; }

        public string RestoreDirectory { get; set; }
        public string NuGetConfigFile { get; set; }
        public IEnumerable<string> Sources { get; set; }
        public IEnumerable<string> FallbackSources { get; set; }
        public bool NoCache { get; set; }
        public bool Lock { get; set; }
        public bool Unlock { get; set; }
        public string PackageFolder { get; set; }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public IApplicationEnvironment ApplicationEnvironment { get; private set; }
        public IMachineWideSettings MachineWideSettings { get; set; }
        public IFileSystem FileSystem { get; set; }
        public Reports Reports { get; set; }
        private Dictionary<string, List<string>> ErrorMessages { get; set; }

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
                else if (!Directory.Exists(RestoreDirectory) && !string.IsNullOrEmpty(RestoreDirectory))
                {
                    throw new InvalidOperationException("The given root is invalid.");
                }

                var restoreDirectory = RestoreDirectory ?? Directory.GetCurrentDirectory();

                var rootDirectory = ProjectResolver.ResolveRootDirectory(restoreDirectory);
                ReadSettings(rootDirectory);

                string packagesDirectory = FeedOptions.TargetPackagesFolder;

                if (string.IsNullOrEmpty(packagesDirectory))
                {
                    packagesDirectory = NuGetDependencyResolver.ResolveRepositoryPath(rootDirectory);
                }

                var packagesFolderFileSystem = CreateFileSystem(packagesDirectory);
                var pathResolver = new DefaultPackagePathResolver(packagesFolderFileSystem, useSideBySidePaths: true);

                int restoreCount = 0;
                int successCount = 0;

                var projectJsonFiles = Directory.EnumerateFiles(
                    restoreDirectory,
                    Runtime.Project.ProjectFileName,
                    SearchOption.AllDirectories);
                Func<string, Task> restorePackage = async projectJsonPath =>
                {
                    Interlocked.Increment(ref restoreCount);
                    var success = await RestoreForProject(projectJsonPath, rootDirectory, packagesDirectory);
                    if (success)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                };

                if (PlatformHelper.IsMono)
                {
                    // Restoring in parallel on Mono throws native exception
                    foreach (var projectJsonFile in projectJsonFiles)
                    {
                        await restorePackage(projectJsonFile);
                    }
                }
                else
                {
                    await ForEachAsync(
                        projectJsonFiles,
                        maxDegreesOfConcurrency: Environment.ProcessorCount,
                        body: restorePackage);
                }

                if (restoreCount > 1)
                {
                    Reports.Information.WriteLine(string.Format("Total time {0}ms", sw.ElapsedMilliseconds));
                }

                foreach(var category in ErrorMessages)
                {
                    Reports.Error.WriteLine("Errors in {0}".Red().Bold(), category.Key);
                    foreach (var message in category.Value)
                    {
                        Reports.Error.WriteLine("    {0}", message);
                    }
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

            var projectFolder = Path.GetDirectoryName(projectJsonPath);
            var projectLockFilePath = Path.Combine(projectFolder, LockFileFormat.LockFileName);

            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(projectJsonPath, out project))
            {
                throw new Exception("TODO: project.json parse error");
            }

            var lockFile = await ReadLockFile(projectLockFilePath);

            var useLockFile = false;
            if (Lock == false &&
                Unlock == false &&
                lockFile != null &&
                lockFile.Islocked)
            {
                useLockFile = true;
            }

            if (useLockFile && !lockFile.IsValidForProject(project))
            {
                // Exhibit the same behavior as if it has been run with "dnu restore --lock"
                Reports.Information.WriteLine("Updating the invalid lock file with {0}",
                    "dnu restore --lock".Yellow().Bold());
                useLockFile = false;
                Lock = true;
            }

            Func<string, string> getVariable = key =>
            {
                return null;
            };

            if (!ScriptExecutor.Execute(project, "prerestore", getVariable))
            {
                ErrorMessages.GetOrAdd("prerestore", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            var projectDirectory = project.ProjectDirectory;
            var projectResolver = new ProjectResolver(projectDirectory, rootDirectory);
            var packageRepository = new PackageRepository(packagesDirectory);
            var restoreOperations = new RestoreOperations(Reports.Verbose);
            var projectProviders = new List<IWalkProvider>();
            var localProviders = new List<IWalkProvider>();
            var remoteProviders = new List<IWalkProvider>();
            var contexts = new List<RestoreContext>();

            projectProviders.Add(
                new LocalWalkProvider(
                    new ProjectReferenceDependencyProvider(
                        projectResolver)));

            localProviders.Add(
                new LocalWalkProvider(
                    new NuGetDependencyResolver(packageRepository)));

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(
                SourceProvider,
                FeedOptions.Sources,
                FeedOptions.FallbackSources);

            AddRemoteProvidersFromSources(remoteProviders, effectiveSources);

            var tasks = new List<Task<GraphNode>>();

            if (useLockFile)
            {
                Reports.Information.WriteLine(string.Format("Following lock file {0}", projectLockFilePath.White().Bold()));

                var context = new RestoreContext
                {
                    FrameworkName = ApplicationEnvironment.RuntimeFramework,
                    ProjectLibraryProviders = projectProviders,
                    LocalLibraryProviders = localProviders,
                    RemoteLibraryProviders = remoteProviders,
                };

                contexts.Add(context);

                foreach (var lockFileLibrary in lockFile.Libraries)
                {
                    var projectLibrary = new LibraryRange
                    {
                        Name = lockFileLibrary.Name,
                        VersionRange = new SemanticVersionRange
                        {
                            MinVersion = lockFileLibrary.Version,
                            MaxVersion = lockFileLibrary.Version,
                            IsMaxInclusive = true,
                            VersionFloatBehavior = SemanticVersionFloatBehavior.None,
                        }
                    };
                    tasks.Add(restoreOperations.CreateGraphNode(context, projectLibrary, _ => false));
                }
            }
            else
            {
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

                foreach (var context in contexts)
                {
                    var projectLibrary = new LibraryRange
                    {
                        Name = project.Name,
                        VersionRange = new SemanticVersionRange(project.Version)
                    };

                    tasks.Add(restoreOperations.CreateGraphNode(context, projectLibrary, _ => true));
                }
            }
            
            var graphs = await Task.WhenAll(tasks);
            foreach (var graph in graphs)
            {
                Reduce(graph);
            }

            if (!useLockFile)
            {
                var runtimeFormatter = new RuntimeFileFormatter();
                var projectRuntimeFile = runtimeFormatter.ReadRuntimeFile(projectJsonPath);
                if (projectRuntimeFile.Runtimes.Any())
                {
                    var runtimeTasks = new List<Task<GraphNode>>();

                    foreach (var pair in contexts.Zip(graphs, (context, graph) => new { context, graph }))
                    {
                        var runtimeFileTasks = new List<Task<RuntimeFile>>();
                        ForEach(pair.graph, node =>
                        {
                            var match = node?.Item?.Match;
                            if (match == null) { return; }
                            runtimeFileTasks.Add(match.Provider.GetRuntimes(node.Item.Match, pair.context.FrameworkName));
                        });

                        var libraryRuntimeFiles = await Task.WhenAll(runtimeFileTasks);
                        var runtimeFiles = new List<RuntimeFile> { projectRuntimeFile };
                        runtimeFiles.AddRange(libraryRuntimeFiles.Where(file => file != null));

                        foreach (var runtimeName in projectRuntimeFile.Runtimes.Keys)
                        {
                            var runtimeSpecs = new List<RuntimeSpec>();
                            FindRuntimeSpecs(
                                runtimeName,
                                runtimeFiles,
                                runtimeSpecs,
                                _ => false);

                            var runtimeContext = new RestoreContext
                            {
                                FrameworkName = pair.context.FrameworkName,
                                ProjectLibraryProviders = pair.context.ProjectLibraryProviders,
                                LocalLibraryProviders = pair.context.LocalLibraryProviders,
                                RemoteLibraryProviders = pair.context.RemoteLibraryProviders,
                                RuntimeName = runtimeName,
                                RuntimeSpecs = runtimeSpecs
                            };
                            var projectLibrary = new LibraryRange
                            {
                                Name = project.Name,
                                VersionRange = new SemanticVersionRange(project.Version)
                            };
                            Reports.Information.WriteLine(string.Format("Graph for {0} on {1}", runtimeContext.FrameworkName, runtimeContext.RuntimeName));
                            runtimeTasks.Add(restoreOperations.CreateGraphNode(runtimeContext, projectLibrary, _ => true));
                        }
                    }

                    var runtimeGraphs = await Task.WhenAll(runtimeTasks);
                    foreach (var runtimeGraph in runtimeGraphs)
                    {
                        Reduce(runtimeGraph);
                    }

                    graphs = graphs.Concat(runtimeGraphs).ToArray();
                }
            }

            var graphItems = new List<GraphItem>();
            var installItems = new List<GraphItem>();
            var missingItems = new HashSet<LibraryRange>();

            ForEach(graphs, node =>
            {
                if (node == null || 
                    node.LibraryRange == null || 
                    node.Disposition == GraphNode.DispositionType.Rejected)
                {
                    return;
                }

                if (node.Item == null || node.Item.Match == null)
                {
                    if (!node.LibraryRange.IsGacOrFrameworkReference &&
                         node.LibraryRange.VersionRange != null &&
                         missingItems.Add(node.LibraryRange))
                    {
                        var errorMessage = string.Format("Unable to locate {0} {1}",
                            node.LibraryRange.Name.Red().Bold(),
                            node.LibraryRange.VersionRange);
                        ErrorMessages.GetOrAdd(projectJsonPath, _ => new List<string>()).Add(errorMessage);
                        Reports.Error.WriteLine(errorMessage);
                        success = false;
                    }

                    return;
                }

                if (!string.Equals(node.Item.Match.Library.Name, node.LibraryRange.Name, StringComparison.Ordinal))
                {
                    // Fix casing of the library name to be installed
                    node.Item.Match.Library.Name = node.LibraryRange.Name;
                }

                var isRemote = remoteProviders.Contains(node.Item.Match.Provider);
                var isInstallItem = installItems.Any(item => item.Match.Library == node.Item.Match.Library);

                if (!isInstallItem && isRemote)
                {
                    installItems.Add(node.Item);
                }

                var isGraphItem = graphItems.Any(item => item.Match.Library == node.Item.Match.Library);
                if (!isGraphItem)
                {
                    graphItems.Add(node.Item);
                }
            });

            await InstallPackages(installItems, packagesDirectory, packageFilter: (library, nupkgSHA) => true);

            if (!useLockFile)
            {
                Reports.Information.WriteLine(string.Format("Writing lock file {0}", projectLockFilePath.White().Bold()));

                // Collect target frameworks
                var frameworks = new HashSet<FrameworkName>();
                foreach (var item in graphItems)
                {
                    Runtime.Project dependencyProject;
                    if (projectProviders.Contains(item.Match.Provider) && projectResolver.TryResolveProject(item.Match.Library.Name, out dependencyProject))
                    {
                        frameworks.AddRange(dependencyProject.GetTargetFrameworks().Select(t => t.FrameworkName));
                    }
                }

                WriteLockFile(projectLockFilePath, project, graphItems, new PackageRepository(packagesDirectory), frameworks);
            }

            if (!ScriptExecutor.Execute(project, "postrestore", getVariable))
            {
                ErrorMessages.GetOrAdd("postrestore", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ScriptExecutor.Execute(project, "prepare", getVariable))
            {
                ErrorMessages.GetOrAdd("prepare", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

            return success;
        }

        private void FindRuntimeSpecs(
            string runtimeName,
            List<RuntimeFile> runtimeFiles,
            List<RuntimeSpec> effectiveRuntimeSpecs,
            Func<string, bool> circularImport)
        {
            effectiveRuntimeSpecs.RemoveAll(spec => spec.Name == runtimeName);

            IEnumerable<string> imports = null;
            foreach (var runtimeFile in runtimeFiles)
            {
                RuntimeSpec runtimeSpec;
                if (runtimeFile.Runtimes.TryGetValue(runtimeName, out runtimeSpec))
                {
                    if (runtimeSpec.Import.Any())
                    {
                        if (imports != null)
                        {
                            throw new Exception(string.Format("More than one runtime.json file has declared imports for {0}", runtimeName));
                        }
                        imports = runtimeSpec.Import;
                    }
                    effectiveRuntimeSpecs.Add(runtimeSpec);
                }
                if (imports != null)
                {
                    foreach (var import in imports)
                    {
                        if (circularImport(import))
                        {
                            if (imports != null)
                            {
                                throw new Exception(string.Format("Circular import for {0}", runtimeName));
                            }
                        }
                        FindRuntimeSpecs(
                            import,
                            runtimeFiles,
                            effectiveRuntimeSpecs,
                            name => string.Equals(name, runtimeName, StringComparison.Ordinal) || circularImport(name));
                    }
                }
            }
        }

        private void Reduce(GraphNode root)
        {
            var patience = 1000;
            var incomplete = true;
            while (incomplete && --patience != 0)
            {
                var tracker = new Tracker();

                // track non-rejected, apply rejection recursively
                ForEach(root, true, (node, state) =>
                {
                    if (!state || node.Disposition == GraphNode.DispositionType.Rejected)
                    {
                        node.Disposition = GraphNode.DispositionType.Rejected;
                    }
                    else
                    {
                        var lib = node?.Item?.Match?.Library;
                        if (lib != null)
                        {
                            tracker.Track(
                                lib.Name,
                                lib.Version);
                        }
                    }
                    return node.Disposition != GraphNode.DispositionType.Rejected;
                });

                // mark items under disputed nodes as ambiguous
                ForEach(root, "Walking", (node, state) =>
                {
                    if (node.Disposition == GraphNode.DispositionType.Rejected)
                    {
                        return "Rejected";
                    }

                    var lib = node?.Item?.Match?.Library;
                    if (lib == null)
                    {
                        return state;
                    }

                    if (state == "Walking" && tracker.IsDisputed(node.Item.Match.Library.Name))
                    {
                        return "Disputed";
                    }

                    if (state == "Disputed")
                    {
                        tracker.MarkAmbiguous(node.Item.Match.Library.Name);
                    }

                    return state;
                });

                // accept or reject nodes that are acceptable and not ambiguous
                ForEach(root, true, (node, state) =>
                {
                    if (!state ||
                        node.Disposition == GraphNode.DispositionType.Rejected ||
                        tracker.IsAmbiguous(node?.Item?.Match?.Library?.Name))
                    {
                        return false;
                    }

                    if (node.Disposition == GraphNode.DispositionType.Acceptable)
                    {
                        var isBestVersion = tracker.IsBestVersion(
                            node?.Item?.Match?.Library?.Name,
                            node?.Item?.Match?.Library?.Version);
                        node.Disposition = isBestVersion ? GraphNode.DispositionType.Accepted : GraphNode.DispositionType.Rejected;
                    }

                    return node.Disposition == GraphNode.DispositionType.Accepted;
                });

                incomplete = false;

                ForEach(root, node => incomplete |= node.Disposition == GraphNode.DispositionType.Acceptable);
            }
        }

        class Tracker
        {
            Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
            class Entry
            {
                public SemanticVersion Version { get; set; }
                public bool IsDisputed { get; set; }
                public bool IsAmbiguous { get; set; }
            }

            private Entry GetEntry(string name)
            {
                Entry entry;
                return _entries.TryGetValue(name, out entry) ? entry : _entries[name] = new Entry();
            }

            internal void Track(string name, SemanticVersion version)
            {
                Entry entry;
                if (_entries.TryGetValue(name, out entry))
                {
                    if (entry.Version != version)
                    {
                        entry.IsDisputed = true;
                        if (entry.Version < version)
                        {
                            entry.Version = version;
                        }
                    }
                }
                else
                {
                    _entries[name] = new Entry { Version = version };
                }
            }

            internal bool IsDisputed(string name)
            {
                return name != null && _entries.ContainsKey(name) && _entries[name].IsDisputed;
            }
            internal bool IsAmbiguous(string name)
            {
                return name != null && _entries.ContainsKey(name) && _entries[name].IsAmbiguous;
            }
            internal void MarkAmbiguous(string name)
            {
                _entries[name].IsAmbiguous = true;
            }

            internal bool IsBestVersion(string name, SemanticVersion version)
            {
                return name == null || _entries[name].Version <= version;
            }
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

        private Task<LockFile> ReadLockFile(string projectLockFilePath)
        {
            if (!File.Exists(projectLockFilePath))
            {
                return Task.FromResult(default(LockFile));
            }
            var lockFileFormat = new LockFileFormat();
            return Task.FromResult(lockFileFormat.Read(projectLockFilePath));
        }

        private void WriteLockFile(string projectLockFilePath, Runtime.Project project, List<GraphItem> graphItems,
            PackageRepository repository, IEnumerable<FrameworkName> frameworks)
        {
            var lockFile = new LockFile();
            lockFile.Islocked = Lock;

            using (var sha512 = SHA512.Create())
            {
                foreach (var item in graphItems.OrderBy(x => x.Match.Library, new LibraryComparer()))
                {
                    var library = item.Match.Library;
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var package = packageInfo.Package;
                    var lockFileLib = LockFileUtils.CreateLockFileLibraryForProject(
                        project,
                        package,
                        sha512,
                        frameworks,
                        new DefaultPackagePathResolver(repository.RepositoryRoot),
                        correctedPackageName: library.Name);

                    lockFile.Libraries.Add(lockFileLib);
                }
            }

            // Use empty string as the key of dependencies shared by all frameworks
            lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                string.Empty,
                project.Dependencies.Select(x => x.LibraryRange.ToString())));

            foreach (var frameworkInfo in project.GetTargetFrameworks())
            {
                lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                    frameworkInfo.FrameworkName.ToString(),
                    frameworkInfo.Dependencies.Select(x => x.LibraryRange.ToString())));
            }

            var lockFileFormat = new LockFileFormat();
            lockFileFormat.Write(projectLockFilePath, lockFile);
        }


        private void AddRemoteProvidersFromSources(List<IWalkProvider> remoteProviders, List<PackageSource> effectiveSources)
        {
            foreach (var source in effectiveSources)
            {
                var feed = PackageSourceUtils.CreatePackageFeed(
                    source,
                    FeedOptions.NoCache,
                    FeedOptions.IgnoreFailedSources,
                    Reports);
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

        void ForEach(GraphNode node, Action<GraphNode> callback)
        {
            callback(node);
            ForEach(node.Dependencies, callback);
        }

        void ForEach(IEnumerable<GraphNode> nodes, Action<GraphNode> callback)
        {
            foreach (var node in nodes)
            {
                ForEach(node, callback);
            }
        }

        void ForEach<TState>(GraphNode node, TState state, Func<GraphNode, TState, TState> callback)
        {
            var childState = callback(node, state);
            ForEach(node.Dependencies, childState, callback);
        }

        void ForEach<TState>(IEnumerable<GraphNode> nodes, TState state, Func<GraphNode, TState, TState> callback)
        {
            foreach (var node in nodes)
            {
                ForEach(node, state, callback);
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

        // Based on http://blogs.msdn.com/b/pfxteam/archive/2012/03/05/10278165.aspx
        private static Task ForEachAsync<TVal>(IEnumerable<TVal> source,
                                               int maxDegreesOfConcurrency,
                                               Func<TVal, Task> body)
        {
            var tasks = Partitioner.Create(source)
                                   .GetPartitions(maxDegreesOfConcurrency)
                                   .AsParallel()
                                   .Select(async partition =>
                                   {
                                       using (partition)
                                       {
                                           while (partition.MoveNext())
                                           {
                                               await body(partition.Current);
                                           }
                                       }
                                   });

            return Task.WhenAll(tasks);
        }
        class LibraryComparer : IComparer<Library>
        {
            public int Compare(Library x, Library y)
            {
                int compare = string.Compare(x.Name, y.Name);
                if (compare == 0)
                {
                    if (x.Version == null && y.Version == null)
                    {
                        // NOOP;
                    }
                    else if (x.Version == null)
                    {
                        compare = -1;
                    }
                    else if (y.Version == null)
                    {
                        compare = 1;
                    }
                    else
                    {
                        compare = x.Version.CompareTo(y.Version);
                    }
                }
                return compare;
            }
        }
    }
}