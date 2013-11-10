using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Loader
{
    public class DefaultHost : IDisposable
    {
        private AssemblyLoader _loader;
        private IFileWatcher _watcher;
        private readonly string _path;

        public DefaultHost(string path, bool watchFiles = true)
        {
            _path = path.TrimEnd(Path.DirectorySeparatorChar);

            CreateDefaultLoader(watchFiles);
        }

        public event Action OnChanged;

        private void OnWatcherChanged()
        {
            if (OnChanged != null)
            {
                OnChanged();
            }
        }

        public Assembly Run()
        {
            string name = GetProjectName();

            var sw = Stopwatch.StartNew();

            var assembly = Assembly.Load(name);

            sw.Stop();

            Trace.TraceInformation("Total load time {0}ms", sw.ElapsedMilliseconds);

            return assembly;
        }

        public void Compile()
        {
            string name = GetProjectName();
            string outputPath = Path.Combine(_path, "bin");

            var sw = Stopwatch.StartNew();

            _loader.Load(new LoadOptions
            {
                AssemblyName = name,
                OutputPath = outputPath
            });

            sw.Stop();

            Trace.TraceInformation("Total compile time {0}ms", sw.ElapsedMilliseconds);
        }

        private string GetProjectName()
        {
            RoslynProject project;
            if (RoslynProject.TryGetProject(_path, out project))
            {
                return project.Name;
            }

            return _path.Substring(Path.GetDirectoryName(_path).Length)
                        .Trim(Path.DirectorySeparatorChar);
        }

        private void CreateDefaultLoader(bool watchFiles)
        {
            _loader = new AssemblyLoader();
            _loader.Attach(AppDomain.CurrentDomain);

            string solutionDir = Path.GetDirectoryName(_path);
            string packagesDir = Path.Combine(solutionDir, "packages");
            string libDir = Path.Combine(solutionDir, "lib");

            if (watchFiles)
            {
                _watcher = new Watcher(solutionDir);
                _watcher.OnChanged += OnWatcherChanged;
            }
            else
            {
                _watcher = Watcher.Noop;
            }

            _loader.Add(new RoslynLoader(solutionDir, _watcher));
            _loader.Add(new MSBuildProjectAssemblyLoader(solutionDir, _watcher));
            _loader.Add(new NuGetAssemblyLoader(packagesDir));
            if (Directory.Exists(libDir))
            {
                _loader.Add(new DirectoryLoader(libDir));
            }
        }

        public void Dispose()
        {
            _loader.Detach(AppDomain.CurrentDomain);
            _watcher.OnChanged -= OnWatcherChanged;
        }
    }
}
