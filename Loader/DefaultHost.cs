using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Loader
{
    public class DefaultHost : IDisposable
    {
        private AssemblyLoader _loader;
        private Watcher _watcher;
        private readonly string _path;

        public DefaultHost(string path)
        {
            _path = path;

            CreateDefaultLoader(path);
        }

        public event Action OnChanged;

        private void OnWatcherChanged()
        {
            if (OnChanged != null)
            {
                OnChanged();
            }
        }

        public void Execute(Action<string> execute)
        {
            ProjectSettings settings;
            if (!ProjectSettings.TryGetSettings(_path, out settings))
            {
                Trace.TraceError("Unable to find " + ProjectSettings.ProjectFileName);
                return;
            }

            try
            {
                var sw = Stopwatch.StartNew();

                execute(settings.Name);

                sw.Stop();

                Trace.TraceInformation("Total load time {0}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Trace.TraceError(String.Join("\n", GetExceptions(ex)));
            }
        }

        private IEnumerable<string> GetExceptions(Exception ex)
        {
            if (ex.InnerException != null)
            {
                foreach (var e in GetExceptions(ex.InnerException))
                {
                    yield return e;
                }
            }

            yield return ex.Message;
        }

        private void CreateDefaultLoader(string path)
        {
            _loader = new AssemblyLoader();
            _loader.Attach(AppDomain.CurrentDomain);

            string solutionDir = Path.GetDirectoryName(path);
            string packagesDir = Path.Combine(solutionDir, "packages");
            string libDir = Path.Combine(solutionDir, "lib");

            _watcher = new Watcher(solutionDir);
            _watcher.OnChanged += OnWatcherChanged;

            _loader.Add(new RoslynLoader(solutionDir, _watcher));
            _loader.Add(new NuGetAssemblyLoader(packagesDir));
            _loader.Add(new MSBuildProjectAssemblyLoader(solutionDir, _watcher));

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
