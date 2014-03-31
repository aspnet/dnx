using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.Net.Project.Packing;
using Microsoft.Net.Runtime;
using NuGet;
using Microsoft.Net.Runtime.Loader.NuGet;

namespace Microsoft.Net.Project
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
            //var globalAssemblyCache = new DefaultGlobalAssemblyCache();
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);

            var nugetDependencyResolver = new NuGetDependencyResolver(projectDir);
            //var gacDependencyExporter = new GacLibraryExportProvider(globalAssemblyCache);
            //var compositeDependencyExporter = new CompositeLibraryExportProvider(new ILibraryExportProvider[] { 
            //    gacDependencyExporter, 
            //    nugetDependencyResolver 
            //});

            var projectReferenceDependencyProvider = new ProjectReferenceDependencyProvider(projectResolver);

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] { 
                projectReferenceDependencyProvider,
                nugetDependencyResolver
            });

            dependencyWalker.Walk(project.Name, project.Version, _options.RuntimeTargetFramework);

            var root = new PackRoot(project, outputPath);

            root.Runtime = new PackRuntime(
                nugetDependencyResolver,
                new Library { Name = "ProjectK", Version = SemanticVersion.Parse("0.1.0-alpha-*") },
                _options.RuntimeTargetFramework);

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
    }
}