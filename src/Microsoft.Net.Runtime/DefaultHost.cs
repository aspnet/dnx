using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.Directory;
using Microsoft.Net.Runtime.Loader.MSBuildProject;
using Microsoft.Net.Runtime.Loader.NuGet;
using Microsoft.Net.Runtime.Loader.Roslyn;

namespace Microsoft.Net.Runtime
{
    public class DefaultHost : IHost
    {
        private AssemblyLoader _loader;
        private IFileWatcher _watcher;
        private readonly string _projectDir;
        private readonly Dictionary<string, object> _hostServices = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public DefaultHost(string projectDir, bool watchFiles = true)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            _projectDir = projectDir.TrimEnd(Path.DirectorySeparatorChar);

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
            Project project;
            if (!Project.TryGetProject(_projectDir, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return null;
            }

            var sw = Stopwatch.StartNew();

            _loader.Walk(project.Name, project.Version, project.TargetFramework);

            var assembly = Assembly.Load(project.Name);

            sw.Stop();

            Trace.TraceInformation("Load took {0}ms", sw.ElapsedMilliseconds);

            return assembly;
        }

        public void Build()
        {
            Project project;
            if (!Project.TryGetProject(_projectDir, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return;
            }

            string outputPath = Path.Combine(_projectDir, "bin");

            var sw = Stopwatch.StartNew();

            _loader.Walk(project.Name, project.Version, project.TargetFramework);

            var asm = _loader.Load(new LoadOptions
            {
                AssemblyName = project.Name,
                OutputPath = outputPath
            });

            if (asm == null)
            {
                Trace.TraceInformation("Unable to compile '{0}'. Try placing a {1} file in the directory.", project.Name, Project.ProjectFileName);
                return;
            }

            RunStaticMethod("Compiler", "Compile", outputPath);

            sw.Stop();

            Trace.TraceInformation("Compile took {0}ms", sw.ElapsedMilliseconds);
        }


        public void Clean()
        {
            Project project;
            if (!Project.TryGetProject(_projectDir, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return;
            }

            string outputPath = Path.Combine(_projectDir, "bin");

            _loader.Walk(project.Name, project.Version, project.TargetFramework);

            var options = new LoadOptions
            {
                AssemblyName = project.Name,
                OutputPath = outputPath,
                CleanArtifacts = new List<string>()
            };

            _loader.Load(options);

            Trace.TraceInformation("Cleaning generated artifacts");

            foreach (var path in options.CleanArtifacts)
            {
                Trace.TraceInformation("Cleaning {0}", path);

                File.Delete(path);
            }

            RunStaticMethod("Compiler", "Clean", outputPath);
        }

        private static void RunStaticMethod(string typeName, string methodName, params object[] args)
        {
            // Invoke a static method on a class with the specified args
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = a.GetType(typeName);

                if (type != null)
                {
                    Trace.TraceInformation("Found {0} in {1}", typeName, a.GetName().Name);

                    var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                    if (method != null)
                    {
                        method.Invoke(null, args);
                    }
                }
            }
        }

        private void Initialize(bool watchFiles)
        {
            _loader = new AssemblyLoader();
            string rootDirectory = ResolveRootDirectory();

            if (watchFiles)
            {
                _watcher = new FileWatcher(rootDirectory);
                _watcher.OnChanged += OnWatcherChanged;
            }
            else
            {
                _watcher = FileWatcher.Noop;
            }

            var resolver = new FrameworkReferenceResolver();
            var roslynLoader = new RoslynAssemblyLoader(rootDirectory, _watcher, resolver);

            var resolved = new HashSet<string>();

            roslynLoader.OnResolveTargetFramework = frameworkName =>
            {
                var path = resolver.GetRuntimeFacadePath(frameworkName);

                if (path != null && resolved.Add(path))
                {
                    _loader.Add(new DirectoryAssemblyLoader(path));
                }
            };

            _loader.Add(roslynLoader);
            _loader.Add(new MSBuildProjectAssemblyLoader(rootDirectory, _watcher));
            _loader.Add(new NuGetAssemblyLoader(_projectDir));

            _hostServices[HostServices.ResolveAssemblyReference] = new Func<string, object>(name =>
            {
                var an = new AssemblyName(name);

                return _loader.ResolveReference(an.Name);
            });
        }

        public Assembly Load(string name)
        {
            return _loader.Load(new LoadOptions
            {
                AssemblyName = name
            });
        }

        public void Dispose()
        {
            _watcher.OnChanged -= OnWatcherChanged;
            _watcher.Dispose();
        }

        private string ResolveRootDirectory()
        {
            var di = new DirectoryInfo(_projectDir);

            if (di.Parent != null)
            {
                if(di.EnumerateFiles("*.sln").Any() ||
                   di.EnumerateDirectories("packages").Any() ||
                   di.EnumerateDirectories(".git").Any())
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            return Path.GetDirectoryName(_projectDir);
        }
    }
}
