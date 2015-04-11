// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.PackageManager.Utils;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.PackageManager.Restore.RuntimeModel;
using NuGet.Configuration;
using NuGet.Client;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.Versioning;
using NuGetDependencyResolver = NuGet.DependencyResolver.NuGetDependencyResolver;
using LockFileFormat = NuGet.ProjectModel.LockFileFormat;
using LockFile = NuGet.ProjectModel.LockFile;
using ProjectFileDependencyGroup = NuGet.ProjectModel.ProjectFileDependencyGroup;
using LibraryRange = NuGet.LibraryModel.LibraryRange;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreCommand
    {
        private static readonly int MaxDegreesOfConcurrency = Environment.ProcessorCount;

        public RestoreCommand()
        {
            MachineWideSettings = new CommandLineMachineWideSettings();
            ScriptExecutor = new ScriptExecutor();
            ErrorMessages = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        }

        public FeedOptions FeedOptions { get; set; }

        public string RestoreDirectory { get; set; }
        public bool NoCache { get; set; }
        public bool Lock { get; set; }
        public bool Unlock { get; set; }
        public string PackageFolder { get; set; }

        /// <summary>
        /// Gets or sets a flag that determines if restore is performed on multiple project.json files in parallel.
        /// </summary>
        public bool Parallel { get; set; }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public IMachineWideSettings MachineWideSettings { get; set; }
        public ILogger Logger { get; set; }
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
                    PackageSpec.PackageSpecFileName,
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
                    packagesDirectory = NuGetRepositoryUtils.ResolveRepositoryPath(rootDirectory);
                }

                int restoreCount = 0;
                int successCount = 0;

                var projectJsonFiles = Directory.EnumerateFiles(
                    restoreDirectory,
                    PackageSpec.PackageSpecFileName,
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
                    Logger.WriteInformation(string.Format("Total time {0}ms", sw.ElapsedMilliseconds));
                }

                foreach (var category in ErrorMessages)
                {
                    Logger.WriteError("Errors in {0}".Red().Bold(), category.Key);
                    foreach (var message in category.Value)
                    {
                        Logger.WriteError("    {0}", message);
                    }
                }

                return restoreCount == successCount;
            }
            catch (Exception ex)
            {
                Logger.WriteInformation("----------");
                Logger.WriteInformation(ex.ToString());
                Logger.WriteInformation("----------");
                Logger.WriteInformation("Restore failed");
                Logger.WriteInformation(ex.Message);
                return false;
            }
        }

        private async Task<bool> RestoreForProject(string projectJsonPath, string rootDirectory, string packagesDirectory)
        {
            var success = true;

            Logger.WriteInformation(string.Format("Restoring packages for {0}", projectJsonPath.Bold()));

            var sw = new Stopwatch();
            sw.Start();

            var projectFolder = Path.GetDirectoryName(projectJsonPath);
            var projectName = new DirectoryInfo(projectFolder).Name;
            var projectLockFilePath = Path.Combine(projectFolder, LockFileFormat.LockFileName);

            PackageSpec packageSpec;
            using (var stream = File.OpenRead(projectJsonPath))
            {
                packageSpec = JsonPackageSpecReader.GetPackageSpec(stream, projectName, projectJsonPath);
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

            if (useLockFile && !lockFile.IsValidForPackageSpec(packageSpec))
            {
                // Exhibit the same behavior as if it has been run with "dnu restore --lock"
                Logger.WriteInformation("Updating the invalid lock file with {0}",
                    "dnu restore --lock".Yellow().Bold());
                useLockFile = false;
                Lock = true;
            }

            Func<string, string> getVariable = key =>
            {
                return null;
            };

            if (!ScriptExecutor.Execute(packageSpec, "prerestore", getVariable))
            {
                ErrorMessages.GetOrAdd("prerestore", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                Logger.WriteError(ScriptExecutor.ErrorMessage);
                return false;
            }

            var projectDirectory = packageSpec.BaseDirectory;
            var packageSpecResolver = new PackageSpecResolver(projectDirectory, rootDirectory);
            var nugetRepository = new NuGetv3LocalRepository(packagesDirectory, checkPackageIdCase: true);

            var context = new RemoteWalkContext();

            context.ProjectLibraryProviders.Add(new LocalDependencyProvider(
                new PackageSpecReferenceDependencyProvider(packageSpecResolver)));

            context.LocalLibraryProviders.Add(
                new LocalDependencyProvider(
                    new NuGetDependencyResolver(nugetRepository)));

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(SourceProvider,
                FeedOptions.Sources, FeedOptions.FallbackSources);

            AddRemoteProvidersFromSources(context.RemoteLibraryProviders, effectiveSources);

            var remoteWalker = new RemoteDependencyWalker(context);

            var tasks = new List<Task<GraphNode<RemoteResolveResult>>>();

            if (useLockFile)
            {
                Logger.WriteInformation(string.Format("Following lock file {0}", projectLockFilePath.White().Bold()));

                foreach (var lockFileLibrary in lockFile.Libraries)
                {
                    var projectLibrary = new LibraryRange
                    {
                        Name = lockFileLibrary.Name,
                        VersionRange = new VersionRange(minVersion: lockFileLibrary.Version,
                            floatRange: new FloatRange(NuGetVersionFloatBehavior.None))
                    };
                    // DNU REFACTORING TODO: replace the hardcoded framework name with IRuntimeEnvironment.RuntimeFramework
                    tasks.Add(remoteWalker.Walk(projectLibrary, new NuGetFramework("DNX,Version=v4.5.1")));
                }
            }
            else
            {
                foreach (var framework in packageSpec.TargetFrameworks)
                {
                    var library = new LibraryRange
                    {
                        Name = packageSpec.Name,
                        VersionRange = new VersionRange(packageSpec.Version),
                        TypeConstraint = LibraryTypes.Project
                    };

                    tasks.Add(remoteWalker.Walk(library, framework.FrameworkName));
                }
            }

            var graphs = await Task.WhenAll(tasks);

            Logger.WriteInformation(string.Format("{0}, {1}ms elapsed", "Resolving complete".Green(), sw.ElapsedMilliseconds));

            if (!useLockFile)
            {
                // DNU REFACTORING TODO: parse 'runtimes' property in project.json
            }

            var libraries = new HashSet<LibraryIdentity>();
            var installItems = new List<GraphItem<RemoteResolveResult>>();
            var missingItems = new HashSet<LibraryRange>();
            var graphItems = new List<GraphItem<RemoteResolveResult>>();

            foreach (var g in graphs)
            {
                g.ForEach(node =>
                {
                    if (node == null || node.Key == null)
                    {
                        return;
                    }

                    if (node.Item == null || node.Item.Data.Match == null)
                    {
                        if (node.Key.TypeConstraint != LibraryTypes.Reference &&
                            node.Key.VersionRange != null &&
                            missingItems.Add(node.Key))
                        {
                            var errorMessage = string.Format("Unable to locate {0} {1}",
                                node.Key.Name.Red().Bold(),
                                node.Key.VersionRange);
                            ErrorMessages.GetOrAdd(projectJsonPath, _ => new List<string>()).Add(errorMessage);
                            Logger.WriteError(errorMessage);
                            success = false;
                        }

                        return;
                    }

                    if (!string.Equals(node.Item.Data.Match.Library.Name, node.Key.Name, StringComparison.Ordinal))
                    {
                        // Fix casing of the library name to be installed
                        node.Item.Data.Match.Library.Name = node.Key.Name;
                    }

                    var isRemote = context.RemoteLibraryProviders.Contains(node.Item.Data.Match.Provider);
                    var isAdded = installItems.Any(item => item.Data.Match.Library == node.Item.Data.Match.Library);

                    if (!isAdded && isRemote)
                    {
                        installItems.Add(node.Item);
                    }

                    var isGraphItem = graphItems.Any(item => item.Data.Match.Library == node.Item.Data.Match.Library);
                    if (!isGraphItem)
                    {
                        graphItems.Add(node.Item);
                    }

                    libraries.Add(node.Item.Key);
                });
            }

            await InstallPackages(installItems, packagesDirectory);

            if (!useLockFile)
            {
                Logger.WriteInformation(string.Format("Writing lock file {0}", projectLockFilePath.White().Bold()));

                // Collect target frameworks
                var frameworks = new HashSet<NuGetFramework>();
                foreach (var item in graphItems)
                {
                    PackageSpec dependencyProject;
                    if (context.ProjectLibraryProviders.Contains(item.Data.Match.Provider) &&
                        packageSpecResolver.TryResolvePackageSpec(item.Data.Match.Library.Name, out dependencyProject))
                    {
                        frameworks.AddRange(dependencyProject.TargetFrameworks.Select(t => t.FrameworkName));
                    }
                }

                WriteLockFile(
                    projectLockFilePath,
                    packageSpec,
                    libraries,
                    new NuGetv3LocalRepository(packagesDirectory, checkPackageIdCase: true),
                    frameworks);
            }

            if (!ScriptExecutor.Execute(packageSpec, "postrestore", getVariable))
            {
                ErrorMessages.GetOrAdd("postrestore", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                Logger.WriteError(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ScriptExecutor.Execute(packageSpec, "prepare", getVariable))
            {
                ErrorMessages.GetOrAdd("prepare", _ => new List<string>()).Add(ScriptExecutor.ErrorMessage);
                Logger.WriteError(ScriptExecutor.ErrorMessage);
                return false;
            }

            Logger.WriteInformation(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

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

        private async Task InstallPackages(List<GraphItem<RemoteResolveResult>> installItems, string packagesDirectory)
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

        private async Task InstallPackageCore(GraphItem<RemoteResolveResult> installItem, string packagesDirectory)
        {
            using (var memoryStream = new MemoryStream())
            {
                var match = installItem.Data.Match;
                await match.Provider.CopyToAsync(installItem.Data.Match, memoryStream);

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

        private void WriteLockFile(
            string projectLockFilePath,
            PackageSpec packageSpec,
            IEnumerable<LibraryIdentity> libraries,
            NuGetv3LocalRepository repository,
            IEnumerable<NuGetFramework> frameworks)
        {
            var lockFile = new LockFile();
            lockFile.Islocked = Lock;

            using (var sha512 = SHA512.Create())
            {
                foreach (var library in libraries.OrderBy(x => x, new LibraryIdentityComparer()))
                {
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var lockFileLib = LockFileUtils.CreateLockFileLibrary(
                        packageInfo,
                        sha512,
                        frameworks,
                        correctedPackageName: library.Name);

                    lockFile.Libraries.Add(lockFileLib);
                }
            }

            // Use empty string as the key of dependencies shared by all frameworks
            lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                string.Empty,
                packageSpec.Dependencies.Select(x => RuntimeStyleLibraryRangeToString(x.LibraryRange))));

            foreach (var frameworkInfo in packageSpec.TargetFrameworks)
            {
                lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                    frameworkInfo.FrameworkName.ToString(),
                    frameworkInfo.Dependencies.Select(x => RuntimeStyleLibraryRangeToString(x.LibraryRange))));
            }

            var lockFileFormat = new LockFileFormat();
            lockFileFormat.Write(projectLockFilePath, lockFile);
        }

        // DNU REFACTORING TODO: temp hack to make generated lockfile work with runtime lockfile validation
        private static string RuntimeStyleLibraryRangeToString(LibraryRange libraryRange)
        {
            var minVersion = libraryRange.VersionRange.MinVersion;
            var maxVersion = libraryRange.VersionRange.MaxVersion;
            var sb = new System.Text.StringBuilder();
            sb.Append(libraryRange.Name);
            sb.Append(" >= ");
            if (libraryRange.VersionRange.IsFloating)
            {
                sb.Append(libraryRange.VersionRange.Float.ToString());
            }
            else
            {
                sb.Append(minVersion.ToString());
            }

            if (maxVersion != null)
            {
                sb.Append(libraryRange.VersionRange.IsMaxInclusive ? "<= " : "< ");
                sb.Append(maxVersion.Version.ToString());
            }

            return sb.ToString();
        }

        private void AddRemoteProvidersFromSources(IList<IRemoteDependencyProvider> remoteProviders, List<PackageSource> effectiveSources)
        {
            foreach (var source in effectiveSources)
            {
                var feed = PackageSourceUtils.CreatePackageFeed(
                    source,
                    FeedOptions.NoCache,
                    FeedOptions.IgnoreFailedSources,
                    Logger);
                if (feed != null)
                {
                    remoteProviders.Add(new RemoteDependencyProvider(feed));
                }
            }
        }

        void ForEach(GraphNode<RemoteResolveResult> node, Action<GraphNode<RemoteResolveResult>> callback)
        {
            callback(node);
            ForEach(node.InnerNodes, callback);
        }

        void ForEach(IEnumerable<GraphNode<RemoteResolveResult>> nodes, Action<GraphNode<RemoteResolveResult>> callback)
        {
            foreach (var node in nodes)
            {
                ForEach(node, callback);
            }
        }

        private void ReadSettings(string rootDirectory)
        {
            Settings = NuGet.Configuration.Settings.LoadDefaultSettings(rootDirectory, configFileName: null,
                machineWideSettings: MachineWideSettings);
            SourceProvider = PackageSourceBuilder.CreateSourceProvider(Settings);
        }

        private bool RestoringInParallel()
        {
            // DNU REFACTORING TODO: enable this after reintroduce the fix:
            // https://github.com/aspnet/dnx/commit/8b032219f737afa2cb436a15fc37580c2df2c6ab
            //return Parallel && !PlatformHelper.IsMono;
            return false;
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

        class LibraryIdentityComparer : IComparer<LibraryIdentity>
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
    }
}