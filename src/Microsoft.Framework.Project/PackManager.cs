// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.Framework.Project.Packing;
using Microsoft.Framework.Runtime;
using NuGet;
using Microsoft.Framework.Runtime.Loader.NuGet;

namespace Microsoft.Framework.Project
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
            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
            var gacDependencyResolver = new GacDependencyResolver();

            var projectReferenceDependencyProvider = new ProjectReferenceDependencyProvider(projectResolver);

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] { 
                projectReferenceDependencyProvider,
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver
            });

            dependencyWalker.Walk(project.Name, project.Version, _options.RuntimeTargetFramework);

            var root = new PackRoot(project, outputPath)
            {
                Overwrite = _options.Overwrite,
                ZipPackages = _options.ZipPackages
            };

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