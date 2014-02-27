using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime.Common.DependencyInjection;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.MSBuildProject;
using Microsoft.Net.Runtime.Loader.NuGet;
using Microsoft.Net.Runtime.Services;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class DefaultHost : IHost
    {
        private AssemblyLoader _loader;
        private DependencyWalker _dependencyWalker;

        private IFileWatcher _watcher;
        private readonly string _projectDir;
        private readonly FrameworkName _targetFramework;
        private readonly string _name;
        private readonly ServiceProvider _serviceProvider = new ServiceProvider();
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private Project _project;

        public DefaultHost(DefaultHostOptions options, IAssemblyLoaderEngine loaderEngine)
        {
            _projectDir = Normalize(options.ApplicationBaseDirectory);

            _name = options.ApplicationName;

            _targetFramework = VersionUtility.ParseFrameworkName(options.TargetFramework ?? "net45");

            _loaderEngine = loaderEngine;

            Initialize(options);
        }

        public IServiceProvider ServiceProvider
        {
            get { return _serviceProvider; }
        }

        public Project Project
        {
            get { return _project; }
        }

        public Assembly GetEntryPoint(string applicationName)
        {
            Trace.TraceInformation("Project root is {0}", _projectDir);

            var sw = Stopwatch.StartNew();

            if (Project == null)
            {
                return null;
            }

            _loader.Walk(Project.Name, Project.Version, _targetFramework);

            _serviceProvider.Add(typeof(IApplicationEnvironment), new ApplicationEnvironment(Project, _targetFramework));
            _dependencyWalker.Walk(project.Name, project.Version, _targetFramework);

            Trace.TraceInformation("Loading entry point from {0}", applicationName);

            var assembly = Assembly.Load(new AssemblyName(applicationName));

            sw.Stop();

            Trace.TraceInformation("Load took {0}ms", sw.ElapsedMilliseconds);

            return assembly;
        }

        public Assembly Load(string name)
        {
            return _loader.LoadAssembly(new LoadContext(name, _targetFramework));
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }

        private void Initialize(DefaultHostOptions options)
        {
            var sp = new ServiceProvider();

            var dependencyProviders = new List<IDependencyProvider>();
            var loaders = new List<IAssemblyLoader>();

            string rootDirectory = ResolveRootDirectory(_projectDir);

            if (options.WatchFiles)
            {
                _watcher = new FileWatcher(rootDirectory);
            }
            else
            {
                _watcher = NoopWatcher.Instance;
            }

            if (!Project.TryGetProject(_projectDir, out _project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
            }

            var projectResolver = new ProjectResolver(_projectDir, rootDirectory);

            var nugetDependencyResolver = new NuGetDependencyResolver(_projectDir);
            var nugetLoader = new NuGetAssemblyLoader(_loaderEngine, nugetDependencyResolver);
            var cachedLoader = new CachedCompilationLoader(_loaderEngine, projectResolver);
            var msbuildLoader = new MSBuildProjectAssemblyLoader(_loaderEngine, rootDirectory, _watcher);

            // Roslyn needs to be able to resolve exported references and sources
            var dependencyExporters = new List<IDependencyExporter>();

            if (options.UseCachedCompilations)
            {
                dependencyExporters.Add(cachedLoader);
            }

            dependencyExporters.Add(nugetDependencyResolver);
            var dependencyExporter = new CompositeDependencyExporter(dependencyExporters);
            var roslynLoader = new LazyRoslynAssemblyLoader(_loaderEngine, projectResolver, _watcher, dependencyExporter);

            // Order is important
            if (options.UseCachedCompilations)
            {
                // Cached compilations
                loaders.Add(cachedLoader);
                dependencyProviders.Add(cachedLoader);
            }

            // Project.json projects
            loaders.Add(roslynLoader);
            dependencyProviders.Add(new ProjectReferenceDependencyProvider(projectResolver));

            // Msbuild project files
            loaders.Add(msbuildLoader);
            // TODO: Add dependency exporter for msbuid

            // NuGet packages
            loaders.Add(nugetLoader);
            dependencyProviders.Add(nugetDependencyResolver);

            _dependencyWalker = new DependencyWalker(dependencyProviders);
            _loader = new AssemblyLoader(loaders);

            _serviceProvider.Add(typeof(IFileMonitor), _watcher);
        }

        public static string ResolveRootDirectory(string projectDir)
        {
            var di = new DirectoryInfo(Path.GetDirectoryName(projectDir));

            while (di.Parent != null)
            {
                if (di.EnumerateFiles("*." + GlobalSettings.GlobalFileName).Any() ||
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

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }
    }
}
