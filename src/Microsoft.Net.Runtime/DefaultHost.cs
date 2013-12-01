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

        public DefaultHost(string projectDir, string targetFramework = "net45", bool watchFiles = true)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            _projectDir = Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));

            _targetFramework = VersionUtility.ParseFrameworkName(targetFramework);

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

        public void Build()
        {
            Project project;
            if (!Project.TryGetProject(_projectDir, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return;
            }

            var sw = Stopwatch.StartNew();

            string outputPath = Path.Combine(_projectDir, "bin");
            string nupkg = GetPackagePath(project, outputPath);

            var configurations = new HashSet<FrameworkName>(
                project.GetTargetFrameworkConfigurations()
                       .Select(c => c.FrameworkName));
            configurations.Add(_targetFramework);

            var builder = new PackageBuilder();

            // TODO: Support nuspecs in the project folder
            builder.Authors.AddRange(project.Authors);

            if (builder.Authors.Count == 0)
            {
                // Temporary
                builder.Authors.Add("K");
            }

            builder.Description = project.Description ?? project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Name;

            bool createPackage = false;

            // Build all target frameworks a project supports
            foreach (var targetFramework in configurations)
            {
                try
                {
                    var result = Build(project, outputPath, targetFramework, builder);

                    if (result != null && result.Errors != null)
                    {
                        Trace.TraceError(String.Join(Environment.NewLine, result.Errors));
                    }
                    else
                    {
                        createPackage = true;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                }
            }

            if (createPackage)
            {
                using (var fs = File.Create(nupkg))
                {
                    builder.Save(fs);
                }

                Trace.TraceInformation("{0} -> {1}", project.Name, nupkg);
            }

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
            string nupkg = GetPackagePath(project, outputPath);

            var configurations = new HashSet<FrameworkName>(
                project.GetTargetFrameworkConfigurations()
                       .Select(c => c.FrameworkName));
            configurations.Add(_targetFramework);

            foreach (var targetFramework in configurations)
            {
                try
                {
                    var result = Clean(project, outputPath, targetFramework);

                    if (result != null && result.Errors != null)
                    {
                        Trace.TraceError(String.Join(Environment.NewLine, result.Errors));
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                }
            }

            if (File.Exists(nupkg))
            {
                Trace.TraceInformation("Cleaning {0}", nupkg);
                File.Delete(nupkg);
            }

            var di = new DirectoryInfo(outputPath);
            DeleteEmptyFolders(di);
        }

        private static void DeleteEmptyFolders(DirectoryInfo di)
        {
            if (!di.Exists)
            {
                return;
            }

            foreach (var d in di.EnumerateDirectories())
            {
                DeleteEmptyFolders(d);
            }

            if (!di.EnumerateFileSystemInfos().Any())
            {
                di.Delete();
            }
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

        private AssemblyLoadResult Build(Project project, string outputPath, FrameworkName targetFramework, PackageBuilder builder)
        {
            _loader.Walk(project.Name, project.Version, targetFramework);

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(targetFramework);
            string targetPath = Path.Combine(outputPath, targetFrameworkFolder);

            var loadContext = new LoadContext(project.Name, targetFramework)
            {
                OutputPath = targetPath,
                ArtifactPaths = new List<string>(),
                PackageBuilder = builder,
            };

            var result = _loader.Load(loadContext);

            if (result == null || result.Errors != null)
            {
                return result;
            }

            // REVIEW: This might not work so well when building for multiple frameworks
            RunStaticMethod("Compiler", "Compile", targetPath);

            return result;
        }

        private AssemblyLoadResult Clean(Project project, string outputPath, FrameworkName targetFramework)
        {
            _loader.Walk(project.Name, project.Version, targetFramework);

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(targetFramework);
            string targetPath = Path.Combine(outputPath, targetFrameworkFolder);

            var loadContext = new LoadContext(project.Name, targetFramework)
            {
                OutputPath = targetPath,
                ArtifactPaths = new List<string>(),
                SkipAssemblyLoad = true
            };

            var result = _loader.Load(loadContext);

            if (result == null || result.Errors != null)
            {
                return result;
            }

            if (loadContext.ArtifactPaths.Count > 0)
            {
                Trace.TraceInformation("Cleaning generated artifacts for {0}", targetFramework);

                foreach (var path in loadContext.ArtifactPaths)
                {
                    if (File.Exists(path))
                    {
                        Trace.TraceInformation("Cleaning {0}", path);

                        File.Delete(path);
                    }
                }
            }

            // REVIEW: This might not work so well when building for multiple frameworks
            RunStaticMethod("Compiler", "Clean", targetPath);

            return result;
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

            var cachedLoader = new CachedCompilationLoader(rootDirectory);
            _loader.Add(cachedLoader);
            var resolver = new FrameworkReferenceResolver();
            var resourceProvider = new ResxResourceProvider();
            var roslynLoader = new RoslynAssemblyLoader(rootDirectory, _watcher, resolver, _loader, resourceProvider);
            _loader.Add(roslynLoader);
            _loader.Add(new MSBuildProjectAssemblyLoader(rootDirectory, _watcher));
            _loader.Add(new NuGetAssemblyLoader(_projectDir));

            _hostServices[HostServices.ResolveAssemblyReference] = new Func<string, object>(name =>
            {
                var an = new AssemblyName(name);

                return _loader.ResolveReference(an.Name);
            });
        }

        private string ResolveRootDirectory()
        {
            var di = new DirectoryInfo(_projectDir);

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

            return Path.GetDirectoryName(_projectDir);
        }

        private static string GetPackagePath(Project project, string outputPath)
        {
            return Path.Combine(outputPath, project.Name + "." + project.Version + ".nupkg");
        }
    }
}
