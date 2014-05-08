using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.PackageManager.Packing;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Loader.NuGet;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackManager
    {
        private readonly PackOptions _options;

        public PackManager(PackOptions options)
        {
            _options = options;
            _options.ProjectDir = Normalize(_options.ProjectDir);
        }

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }

        public bool Package()
        {
            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(_options.ProjectDir, out project))
            {
                Console.WriteLine("Unable to locate {0}.'", Runtime.Project.ProjectFileName);
                return false;
            }

            var sw = Stopwatch.StartNew();

            string outputPath = _options.OutputDir ?? Path.Combine(_options.ProjectDir, "bin", "output");

            var projectDir = project.ProjectDirectory;
            var rootDirectory = DefaultHost.ResolveRootDirectory(projectDir);
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);

            var nugetDependencyResolver = new NuGetDependencyResolver(projectDir);

            var projectReferenceDependencyProvider = new ProjectReferenceDependencyProvider(projectResolver);

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] { 
                projectReferenceDependencyProvider,
                nugetDependencyResolver
            });

            dependencyWalker.Walk(project.Name, project.Version, _options.RuntimeTargetFramework);

            var root = new PackRoot(project, outputPath)
            {
                Overwrite = _options.Overwrite,
                ZipPackages = _options.ZipPackages
            };

            foreach (var runtime in _options.Runtimes)
            {
                if (TryAddRuntime(root, runtime))
                {
                    continue;
                }

                var kreHome = Environment.GetEnvironmentVariable("KRE_HOME");
                if (string.IsNullOrEmpty(kreHome))
                {
                    kreHome = Environment.GetEnvironmentVariable("ProgramFiles") + @"\KRE;%USERPROFILE%\.kre";
                }

                foreach(var portion in kreHome.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var packagesPath = Path.Combine(
                        Environment.ExpandEnvironmentVariables(portion),
                        "packages",
                        runtime);

                    if (TryAddRuntime(root, packagesPath))
                    {
                        break;
                    }
                }
            }

            foreach (var libraryDescription in projectReferenceDependencyProvider.Dependencies)
            {
                root.Projects.Add(new PackProject(projectReferenceDependencyProvider, projectResolver, libraryDescription));
            }

            foreach (var libraryDescription in nugetDependencyResolver.Dependencies)
            {
                root.Packages.Add(new PackPackage(nugetDependencyResolver, libraryDescription));
            }

            root.Emit();

            sw.Stop();

            Console.WriteLine("Time elapsed {0}", sw.Elapsed);
            return true;
        }

        bool TryAddRuntime(PackRoot root, string krePath)
        {
            if (!Directory.Exists(krePath))
            {
                return false;
            }

            var kreName = Path.GetFileName(Path.GetDirectoryName(Path.Combine(krePath, ".")));
            var kreNupkgPath = Path.Combine(krePath, kreName + ".nupkg");
            if (!File.Exists(kreNupkgPath))
            {
                return false;
            }

            root.Runtimes.Add(new PackRuntime(kreNupkgPath));
            return true;
        }
    }
}