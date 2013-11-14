using System;
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

            Initialize(watchFiles);
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

            Trace.TraceInformation("Load took {0}ms", sw.ElapsedMilliseconds);

            return assembly;
        }

        public void Compile()
        {
            string name = GetProjectName();
            string outputPath = Path.Combine(_path, "bin");

            var sw = Stopwatch.StartNew();

            var asm = _loader.Load(new LoadOptions
            {
                AssemblyName = name,
                OutputPath = outputPath
            });

            sw.Stop();

            if (asm == null)
            {
                Trace.TraceInformation("Unable to compile '{0}'. Try placing a project.json file in the directory.", name);
                return;
            }

            Trace.TraceInformation("Compile took {0}ms", sw.ElapsedMilliseconds);
        }

        public void Clean()
        {
            string name = GetProjectName();
            string outputPath = Path.Combine(_path, "bin");

            File.Delete(Path.Combine(outputPath, name + ".dll"));
            File.Delete(Path.Combine(outputPath, name + ".pdb"));
        }

        private string GetProjectName()
        {
            string projectName;
            if (RoslynProject.TryGetProjectName(_path, out projectName))
            {
                return projectName;
            }

            return RoslynProject.GetDirectoryName(_path);
        }

        private void Initialize(bool watchFiles)
        {
            _loader = new AssemblyLoader();
            _loader.Attach(AppDomain.CurrentDomain);

            string solutionDir = Path.GetDirectoryName(_path);
            string packagesDir = Path.Combine(solutionDir, "packages");
            string libDir = Path.Combine(solutionDir, "lib");

            if (watchFiles)
            {
                _watcher = new FileWatcher(solutionDir);
                _watcher.OnChanged += OnWatcherChanged;
            }
            else
            {
                _watcher = FileWatcher.Noop;
            }

            _loader.Add(new RoslynLoader(solutionDir, _watcher, new FrameworkReferenceResolver()));
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
            _watcher.Dispose();
        }
    }
}
