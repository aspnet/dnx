using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NuGet;

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
            Project project;
            if (!Project.TryGetProject(_path, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return null;
            }

            var sw = Stopwatch.StartNew();

            _loader.Walk(project.Name, project.Version);

            var assembly = Assembly.Load(project.Name);

            sw.Stop();

            Trace.TraceInformation("Load took {0}ms", sw.ElapsedMilliseconds);

            return assembly;
        }

        public void Compile()
        {
            Project project;
            if (!Project.TryGetProject(_path, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return;
            }

            string outputPath = Path.Combine(_path, "bin");

            var sw = Stopwatch.StartNew();

            _loader.Walk(project.Name, project.Version);

            var asm = _loader.Load(new LoadOptions
            {
                AssemblyName = project.Name,
                OutputPath = outputPath
            });

            var builder = new PackageBuilder();
            builder.Authors.Add("K");
            builder.Description = project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Name;
            var framework = project.TargetFramework;
            var dependencies = new List<PackageDependency>();

            var resolver = new FrameworkReferenceResolver();
            var frameworkReferences = new HashSet<string>(resolver.GetFrameworkReferences(framework));
            var frameworkAssemblies = new List<string>();

            if (project.Dependencies.Count > 0)
            {
                foreach (var dependency in project.Dependencies)
                {
                    if (frameworkReferences.Contains(dependency.Name))
                    {
                        frameworkAssemblies.Add(dependency.Name);
                    }
                    else
                    {
                        dependencies.Add(new PackageDependency(dependency.Name, new VersionSpec(dependency.Version)));
                    }
                }

                builder.DependencySets.Add(new PackageDependencySet(framework, dependencies));
            }

            foreach (var a in frameworkAssemblies)
            {
                builder.FrameworkReferences.Add(new FrameworkAssemblyReference(a));
            }

            var file = new PhysicalPackageFile();
            file.SourcePath = asm.Location;
            file.TargetPath = "lib\\net45\\" + project.Name + ".dll";
            builder.Files.Add(file);

            string nupkg = Path.Combine(outputPath, project.Name + ".nupkg");

            using (var pkg = File.Create(nupkg))
            {
                builder.Save(pkg);
            }

            sw.Stop();

            if (asm == null)
            {
                Trace.TraceInformation("Unable to compile '{0}'. Try placing a {1} file in the directory.", project.Name, Project.ProjectFileName);
                return;
            }

            Trace.TraceInformation("Compile took {0}ms", sw.ElapsedMilliseconds);
        }

        public void Clean()
        {
            Project project;
            if (!Project.TryGetProject(_path, out project))
            {
                Trace.TraceInformation("Unable to locate {0}.'", Project.ProjectFileName);
                return;
            }

            string outputPath = Path.Combine(_path, "bin");

            File.Delete(Path.Combine(outputPath, project.Name + ".dll"));
            File.Delete(Path.Combine(outputPath, project.Name + ".pdb"));
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
