using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, object> _hostServices = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private Assembly _entryPoint;
        private readonly FrameworkName _targetFramework;

        public DefaultHost(string projectDir, string targetFramework = "net45", bool watchFiles = false)
        {
            _projectDir = Normalize(projectDir);

            _targetFramework = VersionUtility.ParseFrameworkName(Environment.GetEnvironmentVariable("TARGET_FRAMEWORK") ?? targetFramework);

            Initialize(watchFiles);
        }

        public event Action OnChanged;

        // REVIEW: The DI design
        public T GetService<T>(string serviceName)
        {
            object value;
            if (_hostServices.TryGetValue(serviceName, out value))
            {
                return (T)value;
            }

            return default(T);
        }

        private void OnWatcherChanged()
        {
            if (OnChanged != null)
            {
                OnChanged();
            }
        }

        public Assembly GetEntryPoint()
        {
            if (_entryPoint != null)
            {
                return _entryPoint;
            }

            Project project;
            if (!Project.TryGetProject(_projectDir, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return null;
            }

            var sw = Stopwatch.StartNew();

            _loader.Walk(project.Name, project.Version, _targetFramework);

            _entryPoint = _loader.LoadAssembly(new LoadContext(project.Name, _targetFramework));

            sw.Stop();

            Trace.TraceInformation("Load took {0}ms", sw.ElapsedMilliseconds);

            return _entryPoint;
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

        private void Initialize(bool watchFiles)
        {
            _loader = new AssemblyLoader();
            string rootDirectory = ResolveRootDirectory(_projectDir);

#if DESKTOP // CORECLR_TODO: FileSystemWatcher
            if (watchFiles)
            {
                _watcher = new FileWatcher(rootDirectory);
                _watcher.OnChanged += OnWatcherChanged;
            }
            else
#endif
            {
                _watcher = NoopWatcher.Instance;
            }

            var globalAssemblyCache = new DefaultGlobalAssemblyCache();

            var cachedLoader = new CachedCompilationLoader(rootDirectory);
            _loader.Add(cachedLoader);
            var resolver = new FrameworkReferenceResolver(globalAssemblyCache);
            var resourceProvider = new ResxResourceProvider();
            var roslynLoader = new RoslynAssemblyLoader(rootDirectory, _watcher, resolver, globalAssemblyCache, _loader, resourceProvider);
            _loader.Add(roslynLoader);
#if DESKTOP // CORECLR_TODO: Process
            _loader.Add(new MSBuildProjectAssemblyLoader(rootDirectory, _watcher));
#endif
            _loader.Add(new NuGetAssemblyLoader(_projectDir));

            _hostServices[HostServices.ResolveAssemblyReference] = new Func<string, object>(name =>
            {
                var an = new AssemblyName(name);

                return _loader.ResolveReference(an.Name);
            });
        }

        private static string ResolveRootDirectory(string projectDir)
        {
            var di = new DirectoryInfo(Path.GetDirectoryName(projectDir));

            if (di.Parent != null)
            {
                if (di.EnumerateFiles("*.sln").Any() ||
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
