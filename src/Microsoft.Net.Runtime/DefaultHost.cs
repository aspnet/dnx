using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.MSBuildProject;
using Microsoft.Net.Runtime.Loader.NuGet;
using Microsoft.Net.Runtime.Loader.Roslyn;
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

        public DefaultHost(DefaultHostOptions options)
        {
            _projectDir = Normalize(options.ApplicationBaseDirectory);

            _name = options.ApplicationName;

            _targetFramework = VersionUtility.ParseFrameworkName(options.TargetFramework ?? "net45");

            Initialize(options);
        }

        public event Action OnChanged;

        private void OnWatcherChanged()
        {
            if (OnChanged != null)
            {
                OnChanged();
            }
        }

        public Assembly GetEntryPoint()
        {
            Trace.TraceInformation("Project root is {0}", _projectDir);

            Project project;
            if (!Project.TryGetProject(_projectDir, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return null;
            }

            var sw = Stopwatch.StartNew();

            _loader.Walk(project.Name, project.Version, _targetFramework);

            string name = _name ?? project.Name;

            Trace.TraceInformation("Loading entry point from {0}", name);

            var assembly = Assembly.Load(new AssemblyName(name));

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
            _watcher.OnChanged -= OnWatcherChanged;
            _watcher.Dispose();
        }

        private void Initialize(DefaultHostOptions options)
        {
            _loader = new AssemblyLoader();
            string rootDirectory = ResolveRootDirectory(_projectDir);

#if NET45 // CORECLR_TODO: FileSystemWatcher
            if (options.WatchFiles)
            {
                _watcher = new FileWatcher(rootDirectory);
                _watcher.OnChanged += OnWatcherChanged;
            }
            else
#endif
            {
                _watcher = NoopWatcher.Instance;
            }

            var projectResolver = new ProjectResolver(_projectDir, rootDirectory);

            if (options.UseCachedCompilations)
            {
                var cachedLoader = new CachedCompilationLoader(projectResolver);
                _loader.Add(cachedLoader);
            }

            var roslynLoader = new LazyRoslynAssemblyLoader(projectResolver, _watcher, _loader);
            _loader.Add(roslynLoader);
#if NET45 // CORECLR_TODO: Process
            _loader.Add(new MSBuildProjectAssemblyLoader(rootDirectory, _watcher));
#endif
            _loader.Add(new NuGetAssemblyLoader(_projectDir));
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
