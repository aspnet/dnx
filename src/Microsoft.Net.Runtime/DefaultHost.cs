using System;
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
using Microsoft.Net.Runtime.Loader.Roslyn;
using Microsoft.Net.Runtime.Services;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class DefaultHost : IHost
    {
        private AssemblyLoader _loader;
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

            _loader.Walk(Project.Name, Project.Version, _targetFramework);

            _serviceProvider.Add(typeof(IApplicationEnvironment), new ApplicationEnvironment(Project, _targetFramework));

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

            _loader = new AssemblyLoader();
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

            if (options.UseCachedCompilations)
            {
                var cachedLoader = new CachedCompilationLoader(_loaderEngine, projectResolver);
                _loader.Add(cachedLoader);
            }

            var roslynLoader = new LazyRoslynAssemblyLoader(_loaderEngine, projectResolver, _watcher, _loader);
            _loader.Add(roslynLoader);
            _loader.Add(new MSBuildProjectAssemblyLoader(_loaderEngine, rootDirectory, _watcher));
            _loader.Add(new NuGetAssemblyLoader(_loaderEngine, _projectDir));

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
