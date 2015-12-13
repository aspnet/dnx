// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Tooling.Publish;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;
using Microsoft.Dnx.Tooling.Utils;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class RestoreCommand
    {
        private static readonly int MaxDegreesOfConcurrency = Environment.ProcessorCount;

        public RestoreCommand() :
            this(fallbackFramework: null)
        {
        }

        public RestoreCommand(IApplicationEnvironment env) :
            this(env.RuntimeFramework)
        {
        }

        public RestoreCommand(FrameworkName fallbackFramework)
        {
            FallbackFramework = fallbackFramework;
            FileSystem = new PhysicalFileSystem(Directory.GetCurrentDirectory());
            ScriptExecutor = new ScriptExecutor();
            Reports = Reports.Constants.NullReports;
        }

        public FeedOptions FeedOptions { get; set; }

        public List<string> RestoreDirectories { get; } = new List<string>();
        public bool Lock { get; set; }
        public bool Unlock { get; set; }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public List<FrameworkName> TargetFrameworks { get; set; } = new List<FrameworkName>();
        public FrameworkName FallbackFramework { get; set; }
        public IEnumerable<string> RequestedRuntimes { get; set; }
        public IEnumerable<string> FallbackRuntimes { get; set; }
        public IFileSystem FileSystem { get; set; }
        public Reports Reports { get; set; }
        public bool CheckHashFile { get; set; } = true;
        public bool SkipInstall { get; set; }
        public bool SkipRestoreEvents { get; set; }
        public bool IgnoreMissingDependencies { get; set; }

        protected internal NuGetConfig Config { get; set; }

        public async Task<bool> Execute()
        {
            ScriptExecutor.Report = Reports.Information;

            var effectiveRestoreDirs = RestoreDirectories.Where(x => !string.IsNullOrEmpty(x));

            if (!effectiveRestoreDirs.Any())
            {
                effectiveRestoreDirs = new[] { Directory.GetCurrentDirectory() };
            }

            var summary = new SummaryContext();
            var packageFeeds = new PackageFeedCache();
            bool success = true;
            foreach (var dir in effectiveRestoreDirs.Select(Path.GetFullPath).Distinct())
            {
                success &= await Execute(dir, packageFeeds, summary);
            }

            summary.DisplaySummary(Reports);

            return success;
        }

        private async Task<bool> Execute(string restoreDirectory, PackageFeedCache packageFeeds, SummaryContext summary)
        {
            try
            {
                var sw = Stopwatch.StartNew();

                IEnumerable<string> projectJsonFiles;
                if (!RestoreProjectsCollector.Find(restoreDirectory, out projectJsonFiles))
                {
                    var errorMessage = $"The given root {restoreDirectory.Red().Bold()} is invalid.";
                    summary.ErrorMessages.GetOrAdd(restoreDirectory, _ => new List<string>()).Add(errorMessage);
                    Reports.Error.WriteLine(errorMessage);
                    return false;
                }

                var rootDirectory = ProjectResolver.ResolveRootDirectory(restoreDirectory);
                ReadSettings(rootDirectory);

                var settings = Config.Settings as Settings;
                if (settings != null)
                {
                    var configFiles = settings.GetConfigFiles();
                    foreach (var file in configFiles)
                    {
                        summary.InformationMessages.GetOrAdd("NuGet Config files used:", _ => new List<string>()).Add(file);
                    }
                }

                string packagesDirectory = FeedOptions.TargetPackagesFolder;

                if (string.IsNullOrEmpty(packagesDirectory))
                {
                    packagesDirectory = NuGetDependencyResolver.ResolveRepositoryPath(rootDirectory);
                }

                var packagesFolderFileSystem = CreateFileSystem(packagesDirectory);
                var pathResolver = new DefaultPackagePathResolver(packagesDirectory);

                var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(
                    Config.Sources,
                    FeedOptions.Sources,
                    FeedOptions.FallbackSources);

                var remoteProviders = new List<IWalkProvider>();
                AddRemoteProvidersFromSources(remoteProviders, effectiveSources, packageFeeds, summary);

                int restoreCount = 0;
                int successCount = 0;

                Func<string, Task> restorePackage = async projectJsonPath =>
                {
                    Interlocked.Increment(ref restoreCount);
                    var success = await RestoreForProject(projectJsonPath, rootDirectory, packagesDirectory, remoteProviders, summary);
                    if (success)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                };

                if (!RestoringInParallel())
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
                        MaxDegreesOfConcurrency,
                        restorePackage);
                }

                if (restoreCount > 1)
                {
                    Reports.Information.WriteLine(string.Format("Total time {0}ms", sw.ElapsedMilliseconds));
                }

                if (summary.InstallCount > 0)
                {
                    summary.InformationMessages.GetOrAdd("Installed:", _ => new List<string>()).Add($"{summary.InstallCount} package(s) to {packagesDirectory}");
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

        private async Task<bool> RestoreForProject(string projectJsonPath, string rootDirectory, string packagesDirectory, IList<IWalkProvider> remoteProviders, SummaryContext summary)
        {
            var success = true;

            Reports.Information.WriteLine(string.Format("Restoring packages for {0}", projectJsonPath.Bold()));

            var sw = new Stopwatch();
            sw.Start();

            var projectFolder = Path.GetDirectoryName(projectJsonPath);
            var projectLockFilePath = Path.Combine(projectFolder, LockFileFormat.LockFileName);

            Runtime.Project project;
            var diagnostics = new List<DiagnosticMessage>();
            if (!Runtime.Project.TryGetProject(projectJsonPath, out project, diagnostics))
            {
                var errorMessages = diagnostics
                    .Where(x => x.Severity == DiagnosticMessageSeverity.Error)
                    .Select(x => x.Message);

                throw new InvalidOperationException(errorMessages.Any() ?
                    $"Errors occurred when while parsing project.json:{Environment.NewLine}{string.Join(Environment.NewLine, errorMessages)}" :
                    "Invalid project.json");
            }

            if (diagnostics.HasErrors())
            {
                var errorMessages = diagnostics
                    .Where(x => x.Severity == DiagnosticMessageSeverity.Error)
                    .Select(x => x.Message);
                summary.ErrorMessages.GetOrAdd(projectJsonPath, _ => new List<string>()).AddRange(errorMessages);
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

            if (!SkipRestoreEvents)
            {
                if (!ScriptExecutor.Execute(project, "prerestore", getVariable))
                {
                    summary.ErrorMessages.GetOrAdd("prerestore", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                    Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                    return false;
                }
            }

            var projectDirectory = project.ProjectDirectory;
            var projectResolver = new ProjectResolver(projectDirectory, rootDirectory);
            var packageRepository = new PackageRepository(packagesDirectory)
            {
                CheckHashFile = CheckHashFile
            };
            var restoreOperations = new RestoreOperations(Reports.Verbose);
            var projectProviders = new List<IWalkProvider>();
            var localProviders = new List<IWalkProvider>();
            var contexts = new List<RestoreContext>();
            var cache = new Dictionary<LibraryRange, Task<WalkProviderMatch>>();

            projectProviders.Add(
                new LocalWalkProvider(
                    new ProjectReferenceDependencyProvider(
                        projectResolver)));

            localProviders.Add(
                new LocalWalkProvider(
                    new NuGetDependencyResolver(packageRepository)));

            var tasks = new List<Task<TargetContext>>();

            if (useLockFile)
            {
                Reports.Information.WriteLine(string.Format("Following lock file {0}", projectLockFilePath.White().Bold()));

                var context = new RestoreContext
                {
                    FrameworkName = FallbackFramework,
                    ProjectLibraryProviders = projectProviders,
                    LocalLibraryProviders = localProviders,
                    RemoteLibraryProviders = remoteProviders,
                    MatchCache = cache
                };

                contexts.Add(context);

                foreach (var lockFileLibrary in lockFile.PackageLibraries)
                {
                    var projectLibrary = new LibraryRange(lockFileLibrary.Name, frameworkReference: false)
                    {
                        VersionRange = new SemanticVersionRange
                        {
                            MinVersion = lockFileLibrary.Version,
                            MaxVersion = lockFileLibrary.Version,
                            IsMaxInclusive = true,
                            VersionFloatBehavior = SemanticVersionFloatBehavior.None,
                        }
                    };

                    tasks.Add(CreateGraphNode(restoreOperations, context, projectLibrary, _ => false));
                }
            }
            else
            {
                var frameworks = TargetFrameworks.Count == 0 ? project.GetTargetFrameworks().Select(f => f.FrameworkName) : TargetFrameworks;

                foreach (var frameworkName in frameworks)
                {
                    var context = new RestoreContext
                    {
                        FrameworkName = frameworkName,
                        ProjectLibraryProviders = projectProviders,
                        LocalLibraryProviders = localProviders,
                        RemoteLibraryProviders = remoteProviders,
                        MatchCache = cache
                    };
                    contexts.Add(context);
                }

                if (!contexts.Any())
                {
                    contexts.Add(new RestoreContext
                    {
                        FrameworkName = FallbackFramework,
                        ProjectLibraryProviders = projectProviders,
                        LocalLibraryProviders = localProviders,
                        RemoteLibraryProviders = remoteProviders,
                        MatchCache = cache
                    });
                }

                foreach (var context in contexts)
                {
                    var projectLibrary = new LibraryRange(project.Name, frameworkReference: false)
                    {
                        VersionRange = new SemanticVersionRange(project.Version)
                    };

                    tasks.Add(CreateGraphNode(restoreOperations, context, projectLibrary, _ => true));
                }
            }

            var targetContexts = await Task.WhenAll(tasks);
            foreach (var targetContext in targetContexts)
            {
                Reduce(targetContext.Root);
            }

            if (!useLockFile)
            {
                var projectRuntimeFile = RuntimeFile.ParseFromProject(project);
                var restoreRuntimes = GetRestoreRuntimes(projectRuntimeFile.Runtimes.Keys).ToList();
                if (restoreRuntimes.Any())
                {
                    var runtimeTasks = new List<Task<TargetContext>>();

                    foreach (var pair in contexts.Zip(targetContexts, (context, graph) => new { context, graph }))
                    {
                        var runtimeFileTasks = new List<Task<RuntimeFile>>();
                        ForEach(pair.graph.Root, node =>
                        {
                            var match = node?.Item?.Match;
                            if (match == null) { return; }
                            runtimeFileTasks.Add(match.Provider.GetRuntimes(node.Item.Match, pair.context.FrameworkName));
                        });

                        var libraryRuntimeFiles = await Task.WhenAll(runtimeFileTasks);
                        var runtimeFiles = new List<RuntimeFile> { projectRuntimeFile };
                        runtimeFiles.AddRange(libraryRuntimeFiles.Where(file => file != null));

                        foreach (var runtimeName in restoreRuntimes)
                        {
                            Reports.WriteVerbose($"Restoring packages for {pair.context.FrameworkName} on {runtimeName}...");
                            var runtimeDependencies = new Dictionary<string, DependencySpec>();
                            var runtimeNames = new HashSet<string>();
                            var runtimeStopwatch = Stopwatch.StartNew();
                            FindRuntimeDependencies(
                                runtimeName,
                                runtimeFiles,
                                runtimeDependencies,
                                runtimeNames);
                            runtimeStopwatch.Stop();
                            Reports.WriteVerbose($" Scanned Runtime graph in {runtimeStopwatch.ElapsedMilliseconds:0.00}ms");

                            // If there are no runtime specs in the graph, we still want to restore for the specified runtime, so synthesize one
                            if (!runtimeNames.Any(r => r.Equals(runtimeName)))
                            {
                                runtimeNames.Add(runtimeName);
                            }

                            var runtimeContext = new RestoreContext
                            {
                                FrameworkName = pair.context.FrameworkName,
                                ProjectLibraryProviders = pair.context.ProjectLibraryProviders,
                                LocalLibraryProviders = pair.context.LocalLibraryProviders,
                                RemoteLibraryProviders = pair.context.RemoteLibraryProviders,
                                RuntimeName = runtimeName,
                                AllRuntimeNames = runtimeNames,
                                RuntimeDependencies = runtimeDependencies,
                                MatchCache = cache
                            };
                            var projectLibrary = new LibraryRange(project.Name, frameworkReference: false)
                            {
                                VersionRange = new SemanticVersionRange(project.Version)
                            };

                            runtimeTasks.Add(CreateGraphNode(restoreOperations, runtimeContext, projectLibrary, _ => true));
                        }
                    }

                    var runtimeTragetContexts = await Task.WhenAll(runtimeTasks);
                    foreach (var runtimeTargetContext in runtimeTragetContexts)
                    {
                        Reduce(runtimeTargetContext.Root);
                    }

                    targetContexts = targetContexts.Concat(runtimeTragetContexts).ToArray();
                }
            }

            var graphItems = new List<GraphItem>();
            var installItems = new List<GraphItem>();
            var missingItems = new HashSet<LibraryRange>();

            foreach (var context in targetContexts)
            {
                ForEach(context.Root, node =>
                {
                    if (node == null || node.LibraryRange == null)
                    {
                        return;
                    }

                    if (node.Item == null || node.Item.Match == null)
                    {
                        // This is a workaround for #1322. Since we use restore to generate the lock file
                        // after publish, it's possible to fail restore after copying the closure
                        if (!IgnoreMissingDependencies)
                        {
                            if (!node.LibraryRange.IsGacOrFrameworkReference &&
                                 missingItems.Add(node.LibraryRange))
                            {
                                var versionString = node.LibraryRange.VersionRange == null ?
                                    string.Empty :
                                    (" " + node.LibraryRange.VersionRange.ToString());
                                var errorMessage =
                                    $"Unable to locate {DependencyTargets.GetDisplayForTarget(node.LibraryRange.Target)} " +
                                    $"{node.LibraryRange.Name.Red().Bold()}{versionString}";
                                summary.ErrorMessages.GetOrAdd(projectJsonPath, _ => new List<string>()).Add(errorMessage);
                                Reports.Error.WriteLine(errorMessage);
                                success = false;
                            }
                        }

                        return;
                    }

                    if (!string.Equals(node.Item.Match.Library.Name, node.LibraryRange.Name, StringComparison.Ordinal))
                    {
                        // Fix casing of the library name to be installed
                        node.Item.Match.Library = node.Item.Match.Library.ChangeName(node.LibraryRange.Name);
                    }

                    var isRemote = remoteProviders.Contains(node.Item.Match.Provider);
                    var isInstallItem = installItems.Any(item => item.Match.Library == node.Item.Match.Library);

                    if (!isInstallItem && isRemote)
                    {
                        // It's ok to download rejected nodes so we avoid downloading them in the future
                        // The trade off is that subsequent restores avoid going to any remotes
                        installItems.Add(node.Item);
                    }

                    // Don't add rejected nodes since we only want to write reduced nodes
                    // to the lock file
                    if (node.Disposition != GraphNode.DispositionType.Rejected)
                    {
                        var isGraphItem = graphItems.Any(item => item.Match.Library == node.Item.Match.Library);

                        if (!isGraphItem)
                        {
                            graphItems.Add(node.Item);
                        }

                        context.Matches.Add(node.Item.Match);
                    }
                });
            }

            if (!SkipInstall)
            {
                await InstallPackages(installItems, packagesDirectory);
                summary.InstallCount += installItems.Count;
            }

            if (!useLockFile)
            {
                Reports.Information.WriteLine(string.Format("Writing lock file {0}", projectLockFilePath.White().Bold()));

                var repository = new PackageRepository(packagesDirectory);

                WriteLockFile(lockFile,
                              projectLockFilePath,
                              project,
                              graphItems,
                              repository,
                              projectResolver,
                              targetContexts);
            }

            if (!SkipRestoreEvents)
            {
                if (!ScriptExecutor.Execute(project, "postrestore", getVariable))
                {
                    summary.ErrorMessages.GetOrAdd("postrestore", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                    Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                    return false;
                }

                if (!ScriptExecutor.Execute(project, "prepare", getVariable))
                {
                    summary.ErrorMessages.GetOrAdd("prepare", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                    Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                    return false;
                }
            }

            Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

            return success;
        }

        private IEnumerable<string> GetRestoreRuntimes(IEnumerable<string> projectRuntimes)
        {
            var runtimes = Enumerable.Concat(
                RequestedRuntimes ?? Enumerable.Empty<string>(),
                projectRuntimes ?? Enumerable.Empty<string>());
            if (runtimes.Any())
            {
                return runtimes;
            }
            else
            {
                return FallbackRuntimes ?? Enumerable.Empty<string>();
            }
        }

        private async Task<TargetContext> CreateGraphNode(RestoreOperations restoreOperations, RestoreContext context, LibraryRange libraryRange, Func<object, bool> predicate)
        {
            var walkStopwatch = Stopwatch.StartNew();
            var node = await restoreOperations.CreateGraphNode(context, libraryRange, predicate);
            Reports.WriteVerbose($" Walked graph for {context.FrameworkName}/{context.RuntimeName} in {walkStopwatch.ElapsedMilliseconds:0.00}ms");
            walkStopwatch.Stop();
            return new TargetContext
            {
                RestoreContext = context,
                Root = node
            };
        }

        internal static void FindRuntimeDependencies(
            string runtimeName,
            List<RuntimeFile> runtimeFiles,
            Dictionary<string, DependencySpec> effectiveRuntimeSpecs,
            HashSet<string> allRuntimeNames)
        {
            FindRuntimeDependencies(runtimeName, runtimeFiles, effectiveRuntimeSpecs, allRuntimeNames, _ => false);
        }

        private static void FindRuntimeDependencies(
            string runtimeName,
            List<RuntimeFile> runtimeFiles,
            Dictionary<string, DependencySpec> effectiveRuntimeSpecs,
            HashSet<string> allRuntimeNames,
            Func<string, bool> circularImport)
        {
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
                            throw new ArgumentException($"More than one runtime.json file has declared imports for '{runtimeName}'", nameof(runtimeName));
                        }
                        imports = runtimeSpec.Import;
                    }

                    allRuntimeNames.Add(runtimeSpec.Name);

                    // Load dependencies provided by this runtime file
                    foreach (var dependencySpec in runtimeSpec.Dependencies.Values)
                    {
                        // The first one to set a spec for a package wins. Why?
                        //  1) The runtime files are coming in order from the top of the dependency graph to the bottom, so
                        //       earlier items in the graph are closer to the root. Having said that, it is NOT EXPECTED that
                        //       a single package be affected by two runtime.json files anyway.
                        //  2) The runtime imports are coming in order from most specific to least specific, so we stop as soon
                        //       as we find a match compatible with our runtime.
                        if (!effectiveRuntimeSpecs.ContainsKey(dependencySpec.Name))
                        {
                            effectiveRuntimeSpecs[dependencySpec.Name] = dependencySpec;
                        }
                    }
                }
            }

            // Add the embedded runtime data if we didn't find any imports
            if (imports == null)
            {
                // This will return null if there are no embedded imports for this runtime, which is fine.
                imports = EmbeddedRuntimeData.GetEmbeddedImports(runtimeName);
            }

            if (imports != null)
            {
                foreach (var import in imports)
                {
                    if (circularImport(import))
                    {
                        if (imports != null)
                        {
                            throw new ArgumentException($"Circular import for '{runtimeName}'", nameof(runtimeName));
                        }
                    }
                    allRuntimeNames.Add(import);
                    FindRuntimeDependencies(
                        import,
                        runtimeFiles,
                        effectiveRuntimeSpecs,
                        allRuntimeNames,
                        name => string.Equals(name, runtimeName, StringComparison.Ordinal) || circularImport(name));
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

        private async Task InstallPackages(List<GraphItem> installItems, string packagesDirectory)
        {
            if (RestoringInParallel())
            {
                await ForEachAsync(
                    installItems,
                    MaxDegreesOfConcurrency,
                    item => InstallPackageCore(item, packagesDirectory));
            }
            else
            {
                foreach (var item in installItems)
                {
                    await InstallPackageCore(item, packagesDirectory);
                }
            }
        }

        private async Task InstallPackageCore(GraphItem installItem, string packagesDirectory)
        {
            using (var memoryStream = new MemoryStream())
            {
                var match = installItem.Match;
                await match.Provider.CopyToAsync(installItem.Match, memoryStream);

                memoryStream.Seek(0, SeekOrigin.Begin);
                await NuGetPackageUtils.InstallFromStream(memoryStream, match.Library, packagesDirectory, Reports.Information);
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

        private void WriteLockFile(LockFile previousLockFile,
                                   string projectLockFilePath,
                                   Runtime.Project project,
                                   List<GraphItem> graphItems,
                                   PackageRepository repository,
                                   IProjectResolver projectResolver,
                                   IEnumerable<TargetContext> contexts)
        {
            var resolver = new DefaultPackagePathResolver(repository.RepositoryRoot.Root);
            var previousPackageLibraries = previousLockFile?.PackageLibraries.ToDictionary(l => Tuple.Create(l.Name, l.Version));

            var lockFile = new LockFile();
            lockFile.Islocked = Lock;

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

            // Record all libraries used
            foreach (var item in graphItems.OrderBy(x => x.Match.Library, new LibraryComparer()))
            {
                if (item.Match.LibraryType.Equals(Runtime.LibraryTypes.Implicit))
                {
                    continue;
                }

                var library = item.Match.Library;
                if (library.Name == project.Name)
                {
                    continue;
                }

                if (item.Match.LibraryType.Equals(Runtime.LibraryTypes.Project))
                {
                    var projectDependency = projectResolver.FindProject(library.Name);
                    var projectLibrary = LockFileUtils.CreateLockFileProjectLibrary(project, projectDependency);

                    lockFile.ProjectLibraries.Add(projectLibrary);
                }
                else if (item.Match.LibraryType.Equals(Runtime.LibraryTypes.Package))
                {
                    var packageInfo = repository.FindPackagesById(library.Name)
                                                .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        throw new InvalidOperationException($"Unresolved package: {library.Name}");
                    }

                    LockFilePackageLibrary previousLibrary = null;
                    previousPackageLibraries?.TryGetValue(Tuple.Create(library.Name, library.Version), out previousLibrary);

                    var package = packageInfo.Package;

                    // The previousLibrary can't be a project, otherwise exception has been thrown.
                    lockFile.PackageLibraries.Add(LockFileUtils.CreateLockFilePackageLibrary(
                        previousLibrary,
                        resolver,
                        package,
                        correctedPackageName: library.Name));
                }
                else
                {
                    throw new InvalidOperationException($"Unresolved library: {library.Name}");
                }
            }

            var packageLibraries = lockFile.PackageLibraries.ToDictionary(lib => Tuple.Create(lib.Name, lib.Version));

            // Add the contexts
            foreach (var context in contexts)
            {
                var target = new LockFileTarget();
                target.TargetFramework = context.RestoreContext.FrameworkName;
                target.RuntimeIdentifier = context.RestoreContext.RuntimeName;

                foreach (var match in context.Matches.OrderBy(x => x.Library, new LibraryComparer()))
                {
                    if (match.Library.Name == project.Name)
                    {
                        continue;
                    }

                    if (match.LibraryType.Equals(Runtime.LibraryTypes.Project))
                    {
                        var projectDependency = projectResolver.FindProject(match.Library.Name);
                        var projectTargetLibrary = LockFileUtils.CreateLockFileTargetLibrary(projectDependency, context.RestoreContext);
                        target.Libraries.Add(projectTargetLibrary);
                    }
                    else if (match.LibraryType.Equals(Runtime.LibraryTypes.Package))
                    {
                        var packageInfo = repository.FindPackagesById(match.Library.Name)
                                                    .FirstOrDefault(p => p.Version == match.Library.Version);

                        var package = packageInfo.Package;

                        target.Libraries.Add(LockFileUtils.CreateLockFileTargetLibrary(
                            packageLibraries[Tuple.Create(match.Library.Name, match.Library.Version)],
                            package,
                            context.RestoreContext,
                            correctedPackageName: match.Library.Name));
                    }
                }

                lockFile.Targets.Add(target);
            }

            var lockFileFormat = new LockFileFormat();
            lockFileFormat.Write(projectLockFilePath, lockFile);
        }

        private void AddRemoteProvidersFromSources(List<IWalkProvider> remoteProviders, List<PackageSource> effectiveSources, PackageFeedCache packageFeeds, SummaryContext summary)
        {
            foreach (var source in effectiveSources)
            {
                var feed = packageFeeds.GetPackageFeed(
                    source,
                    FeedOptions.NoCache,
                    FeedOptions.IgnoreFailedSources,
                    Reports);
                if (feed != null)
                {
                    remoteProviders.Add(new RemoteWalkProvider(feed));
                    var list = summary.InformationMessages.GetOrAdd("Feeds used:", _ => new List<string>());
                    if (!list.Contains(feed.Source))
                    {
                        list.Add(feed.Source);
                    }
                }
            }
        }

        private static void ExtractPackage(string targetPath, FileStream stream)
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var packOperations = new PublishOperations();
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
            Config = NuGetConfig.ForSolution(solutionDirectory, FileSystem);
        }

        private IFileSystem CreateFileSystem(string path)
        {
            path = FileSystem.GetFullPath(path);
            return new PhysicalFileSystem(path);
        }

        private bool RestoringInParallel()
        {
            return FeedOptions.Parallel && !RuntimeEnvironmentHelper.IsMono;
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

        class LibraryComparer : IComparer<LibraryIdentity>
        {
            public int Compare(LibraryIdentity x, LibraryIdentity y)
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

        private class TargetContext
        {
            public RestoreContext RestoreContext { get; set; }

            public HashSet<WalkProviderMatch> Matches { get; set; } = new HashSet<WalkProviderMatch>();

            public GraphNode Root { get; set; }
        }

        private class SummaryContext
        {
            public Dictionary<string, List<string>> ErrorMessages = new Dictionary<string, List<string>>();
            public Dictionary<string, List<string>> InformationMessages = new Dictionary<string, List<string>>();
            public int InstallCount;

            public void DisplaySummary(Reports reports)
            {
                foreach (var category in ErrorMessages)
                {
                    reports.Error.WriteLine($"{Environment.NewLine}Errors in {category.Key}".Red().Bold());
                    foreach (var message in category.Value)
                    {
                        reports.Error.WriteLine($"    {message}");
                    }
                }

                foreach (var category in InformationMessages)
                {
                    reports.Quiet.WriteLine($"{Environment.NewLine}{category.Key}");
                    foreach (var message in category.Value)
                    {
                        reports.Quiet.WriteLine($"    {message}");
                    }
                }
            }
        }
    }
}
