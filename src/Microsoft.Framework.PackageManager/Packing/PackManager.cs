// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.PackageManager.Packing;
using Microsoft.Framework.Runtime;
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

        public class DependencyContext
        {
            public DependencyContext(string projectDir)
            {
                var rootDirectory = ProjectResolver.ResolveRootDirectory(projectDir);
                var projectResolver = new ProjectResolver(projectDir, rootDirectory);

                var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
                var nugetDependencyResolver = new NuGetDependencyResolver(projectDir, referenceAssemblyDependencyResolver.FrameworkResolver);
                var gacDependencyResolver = new GacDependencyResolver();
                var projectReferenceDependencyProvider = new ProjectReferenceDependencyProvider(projectResolver);

                var dependencyWalker = new DependencyWalker(new IDependencyProvider[] { 
                    projectReferenceDependencyProvider,
                    referenceAssemblyDependencyResolver,
                    gacDependencyResolver,
                    nugetDependencyResolver
                });

                ProjectResolver = projectResolver;
                NuGetDependencyResolver = nugetDependencyResolver;
                ProjectReferenceDependencyProvider = projectReferenceDependencyProvider;
                DependencyWalker = dependencyWalker;
            }

            public ProjectResolver ProjectResolver { get; set; }
            public NuGetDependencyResolver NuGetDependencyResolver { get; set; }
            public ProjectReferenceDependencyProvider ProjectReferenceDependencyProvider { get; set; }
            public DependencyWalker DependencyWalker { get; set; }
            public FrameworkName FrameworkName { get; set; }

            public void Walk(string projectName, SemanticVersion projectVersion, FrameworkName frameworkName)
            {
                FrameworkName = frameworkName;
                DependencyWalker.Walk(projectName, projectVersion, frameworkName);
            }

            public static FrameworkName GetFrameworkNameForRuntime(string runtime)
            {
                var parts = runtime.Split(new[] { '.' }, 2);
                if (parts.Length != 2)
                {
                    return null;
                }
                parts = parts[0].Split(new[] { '-' }, 3);
                if (parts.Length != 3)
                {
                    return null;
                }
                switch (parts[1])
                {
                    case "svr50":
                        return VersionUtility.ParseFrameworkName("net451");
                    case "svrc50":
                        return VersionUtility.ParseFrameworkName("k10");
                }
                return null;
            }
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

            var dependencyContexts = new List<DependencyContext>();

            var root = new PackRoot(project, outputPath)
            {
                Overwrite = _options.Overwrite,
                ZipPackages = _options.ZipPackages,
                AppFolder = _options.AppFolder ?? project.Name,
            };

            foreach (var runtime in _options.Runtimes)
            {
                var runtimeLocated = TryAddRuntime(root, runtime);

                var kreHome = Environment.GetEnvironmentVariable("KRE_HOME");
                if (string.IsNullOrEmpty(kreHome))
                {
                    kreHome = Environment.GetEnvironmentVariable("ProgramFiles") + @"\KRE;%USERPROFILE%\.kre";
                }

                foreach (var portion in kreHome.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var packagesPath = Path.Combine(
                        Environment.ExpandEnvironmentVariables(portion),
                        "packages",
                        runtime);

                    if (TryAddRuntime(root, packagesPath))
                    {
                        runtimeLocated = true;
                        break;
                    }
                }

                if (!runtimeLocated)
                {
                    Console.WriteLine(string.Format("Unable to locate runtime '{0}'", runtime));
                    return false;
                }

                var frameworkName = DependencyContext.GetFrameworkNameForRuntime(Path.GetFileName(runtime));
                if (!dependencyContexts.Any(dc => dc.FrameworkName == frameworkName))
                {
                    var dependencyContext = new DependencyContext(projectDir);
                    dependencyContext.Walk(project.Name, project.Version, frameworkName);
                    dependencyContexts.Add(dependencyContext);
                }
            }

            if (!dependencyContexts.Any())
            {
                var frameworkName = DependencyContext.GetFrameworkNameForRuntime("KRE-svr50-x86.*");
                var dependencyContext = new DependencyContext(projectDir);
                dependencyContext.Walk(project.Name, project.Version, frameworkName);
                dependencyContexts.Add(dependencyContext);
            }

            foreach (var dependencyContext in dependencyContexts)
            {
                foreach (var libraryDescription in dependencyContext.NuGetDependencyResolver.Dependencies)
                {
                    if (!root.Packages.Any(p => p.Library == libraryDescription.Identity))
                    {
                        root.Packages.Add(new PackPackage(dependencyContext.NuGetDependencyResolver, libraryDescription));
                    }
                }
                foreach (var libraryDescription in dependencyContext.ProjectReferenceDependencyProvider.Dependencies)
                {
                    if (!root.Projects.Any(p => p.Name == libraryDescription.Identity.Name))
                    {
                        var packProject = new PackProject(dependencyContext.ProjectReferenceDependencyProvider, dependencyContext.ProjectResolver, libraryDescription);
                        if (packProject.Name == project.Name)
                        {
                            packProject.AppFolder = _options.AppFolder;
                        }
                        root.Projects.Add(packProject);
                    }
                }
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