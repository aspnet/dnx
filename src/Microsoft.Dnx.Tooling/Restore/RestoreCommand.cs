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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Tooling.Publish;
using Microsoft.Dnx.Tooling.Restore;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;
using Microsoft.Dnx.Tooling.Utils;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime.DependencyManagement;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class RestoreCommand
    {
        private static readonly int MaxDegreesOfConcurrency = Environment.ProcessorCount;

        private int _installCount;

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
            ErrorMessages = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            InformationMessages = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Reports = new Reports
            {
                Information = new NullReport(),
                Error = new NullReport(),
                Quiet = new NullReport(),
                Verbose = new NullReport()
            };
        }

        public FeedOptions FeedOptions { get; set; }

        public List<string> RestoreDirectories { get; } = new List<string>();
        public bool Lock { get; set; }
        public bool Unlock { get; set; }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public List<FrameworkName> TargetFrameworks { get; set; } = new List<FrameworkName>();
        public FrameworkName FallbackFramework { get; set; }
        public IFileSystem FileSystem { get; set; }
        public Reports Reports { get; set; }
        public bool CheckHashFile { get; set; } = true;
        public bool SkipInstall { get; set; }
        public bool SkipRestoreEvents { get; set; }
        public bool IgnoreMissingDependencies { get; set; }

        private Dictionary<string, List<string>> ErrorMessages { get; set; }
        private Dictionary<string, List<string>> InformationMessages { get; set; }

        protected internal NuGetConfig Config { get; set; }

        public async Task<bool> Execute()
        {
            ScriptExecutor.Report = Reports.Information;

            var effectiveRestoreDirs = RestoreDirectories.Where(x => !string.IsNullOrEmpty(x));

            if (!effectiveRestoreDirs.Any())
            {
                effectiveRestoreDirs = new[] { Directory.GetCurrentDirectory() };
            }

            bool success = true;
            foreach (var dir in effectiveRestoreDirs.Select(Path.GetFullPath).Distinct())
            {
                success &= await Execute(dir);
            }

            foreach (var category in ErrorMessages)
            {
                Reports.Error.WriteLine($"{Environment.NewLine}Errors in {category.Key}".Red().Bold());
                foreach (var message in category.Value)
                {
                    Reports.Error.WriteLine($"    {message}");
                }
            }

            var settings = Config.Settings as Settings;
            if (settings != null)
            {
                var configFiles = settings.GetConfigFiles();

                // target for PR
                Reports.Quiet.WriteLine($"{Environment.NewLine}NuGet Config files used:");
                foreach (var file in configFiles)
                {
                    Reports.Quiet.WriteLine($"    {file}");
                }
            }

            foreach (var category in InformationMessages)
            {
                Reports.Quiet.WriteLine($"{Environment.NewLine}{category.Key}");
                foreach(var message in category.Value)
                {
                    Reports.Quiet.WriteLine($"    {message}");
                }
            }

            return success;
        }

        private async Task<bool> Execute(string restoreDirectory)
        {
            try
            {
                var sw = Stopwatch.StartNew();

                var projectJsonFiles = new List<string>();

                if (string.Equals(
                    Runtime.Project.ProjectFileName,
                    Path.GetFileName(restoreDirectory),
                    StringComparison.OrdinalIgnoreCase) && File.Exists(restoreDirectory))
                {
                    // If the path is a project.json file we don't do recursive search in subfolders
                    projectJsonFiles.Add(restoreDirectory);
                }
                else if (Directory.Exists(restoreDirectory))
                {
                    var projectJsonFile = Path.Combine(restoreDirectory, Runtime.Project.ProjectFileName);
                    if (File.Exists(projectJsonFile))
                    {
                        // If the path contains a project.json file we don't do recursive search in subfolders
                        projectJsonFiles.Add(projectJsonFile);
                    }
                    else
                    {
                        projectJsonFiles.AddRange(Directory.EnumerateFiles(
                            restoreDirectory,
                            Runtime.Project.ProjectFileName,
                            SearchOption.AllDirectories));
                    }
                }
                else
                {
                    var errorMessage = $"The given root {restoreDirectory.Red().Bold()} is invalid.";
                    ErrorMessages.GetOrAdd(restoreDirectory, _ => new List<string>()).Add(errorMessage);
                    Reports.Error.WriteLine(errorMessage);
                    return false;
                }

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

                Func<string, Task> restorePackage = async projectJsonPath =>
                {
                    Interlocked.Increment(ref restoreCount);
                    var success = await RestoreForProject(projectJsonPath, rootDirectory, packagesDirectory);
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

                if (_installCount > 0)
                {
                    InformationMessages.GetOrAdd("Installed:", _ => new List<string>()).Add($"{_installCount} package(s) to {packagesDirectory}");
                    _installCount = 0;
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
            var diagnostics = new List<DiagnosticMessage>();
            if (!Runtime.Project.TryGetProject(projectJsonPath, out project, diagnostics))
            {
                throw new Exception("TODO: project.json parse error");
            }

            if (diagnostics.HasErrors())
            {
                var errorMessages = diagnostics
                    .Where(x => x.Severity == DiagnosticMessageSeverity.Error)
                    .Select(x => x.Message);
                ErrorMessages.GetOrAdd(projectJsonPath, _ => new List<string>()).AddRange(errorMessages);
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
                    ErrorMessages.GetOrAdd("prerestore", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
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
            var remoteProviders = new List<IWalkProvider>();
            var contexts = new List<RestoreContext>();
            var cache = new Dictionary<LibraryRange, Task<WalkProviderMatch>>();

            projectProviders.Add(
                new LocalWalkProvider(
                    new ProjectReferenceDependencyProvider(
                        projectResolver)));

            localProviders.Add(
                new LocalWalkProvider(
                    new NuGetDependencyResolver(packageRepository)));

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(
                Config.Sources,
                FeedOptions.Sources,
                FeedOptions.FallbackSources);

            AddRemoteProvidersFromSources(remoteProviders, effectiveSources);

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

                foreach (var lockFileLibrary in lockFile.Libraries)
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
                var runtimeFormatter = new RuntimeFileFormatter();
                var projectRuntimeFile = runtimeFormatter.ReadRuntimeFile(projectJsonPath);
                if (projectRuntimeFile.Runtimes.Any())
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
                                RuntimeSpecs = runtimeSpecs,
                                MatchCache = cache
                            };
                            var projectLibrary = new LibraryRange(project.Name, frameworkReference: false)
                            {
                                VersionRange = new SemanticVersionRange(project.Version)
                            };
                            Reports.Information.WriteLine(string.Format("Graph for {0} on {1}", runtimeContext.FrameworkName, runtimeContext.RuntimeName));
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

                        context.Libraries.Add(node.Item.Match.Library);
                    }
                });
            }

            if (!SkipInstall)
            {
                await InstallPackages(installItems, packagesDirectory);
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
                              targetContexts);
            }

            if (!SkipRestoreEvents)
            {
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
            }

            Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

            return success;
        }

        private async Task<TargetContext> CreateGraphNode(RestoreOperations restoreOperations, RestoreContext context, LibraryRange libraryRange, Func<object, bool> predicate)
        {
            var node = await restoreOperations.CreateGraphNode(context, libraryRange, predicate);
            return new TargetContext
            {
                RestoreContext = context,
                Root = node
            };
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
                Interlocked.Increment(ref _installCount);
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
                                   IEnumerable<TargetContext> contexts)
        {
            var resolver = new DefaultPackagePathResolver(repository.RepositoryRoot);
            var previousLibraries = previousLockFile?.Libraries.ToDictionary(l => Tuple.Create(l.Name, l.Version));

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
                var library = item.Match.Library;
                var packageInfo = repository.FindPackagesById(library.Name)
                    .FirstOrDefault(p => p.Version == library.Version);

                if (packageInfo == null)
                {
                    continue;
                }

                var package = packageInfo.Package;

                LockFileLibrary previousLibrary = null;
                previousLibraries?.TryGetValue(Tuple.Create(library.Name, library.Version), out previousLibrary);

                var lockFileLib = LockFileUtils.CreateLockFileLibrary(
                    previousLibrary,
                    resolver,
                    package,
                    correctedPackageName: library.Name);

                lockFile.Libraries.Add(lockFileLib);
            }

            var libraries = lockFile.Libraries.ToDictionary(lib => Tuple.Create(lib.Name, lib.Version));

            // Add the contexts
            foreach (var context in contexts)
            {
                var target = new LockFileTarget();
                target.TargetFramework = context.RestoreContext.FrameworkName;
                target.RuntimeIdentifier = context.RestoreContext.RuntimeName;

                foreach (var library in context.Libraries.OrderBy(x => x, new LibraryComparer()))
                {
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var package = packageInfo.Package;

                    var targetLibrary = LockFileUtils.CreateLockFileTargetLibrary(
                        libraries[Tuple.Create(library.Name, library.Version)],
                        package,
                        context.RestoreContext,
                        correctedPackageName: library.Name);

                    target.Libraries.Add(targetLibrary);
                }

                lockFile.Targets.Add(target);
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
                    // target for PR
                    var list = InformationMessages.GetOrAdd("Feeds used:", _ => new List<string>());
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

            public HashSet<LibraryIdentity> Libraries { get; set; } = new HashSet<LibraryIdentity>();

            public GraphNode Root { get; set; }
        }
    }
}
