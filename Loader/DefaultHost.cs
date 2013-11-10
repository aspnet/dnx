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
            _path = path.TrimEnd(Path.DirectorySeparatorChar);

            CreateDefaultLoader();
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
            catch (FileNotFoundException)
            {

            }
            catch (Exception ex)
            {
                Trace.TraceError(String.Join("\n", GetExceptions(ex)));
            }
        }

        public void Compile(string outputPath)
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

                _loader.Load(new LoadOptions
                {
                    AssemblyName = settings.Name,
                    OutputPath = Path.Combine(_path, "bin")
                });

                sw.Stop();

                Trace.TraceInformation("Total load time {0}ms", sw.ElapsedMilliseconds);
            }
            catch (FileNotFoundException)
            {

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

            yield return ex.ToString();
        }

        private void CreateDefaultLoader()
        {
            _loader = new AssemblyLoader();
            _loader.Attach(AppDomain.CurrentDomain);

            string solutionDir = Path.GetDirectoryName(_path);
            string packagesDir = Path.Combine(solutionDir, "packages");
            string libDir = Path.Combine(solutionDir, "lib");

            _watcher = new Watcher(solutionDir);
            _watcher.OnChanged += OnWatcherChanged;

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
