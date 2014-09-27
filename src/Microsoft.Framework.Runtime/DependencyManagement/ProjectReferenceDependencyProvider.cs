// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class ProjectReferenceDependencyProvider : IDependencyProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IFrameworkReferenceResolver _frameworkReferenceResolver;

        public ProjectReferenceDependencyProvider(IProjectResolver projectResolver, IFrameworkReferenceResolver frameworkReferenceResolver)
        {
            _projectResolver = projectResolver;
            _frameworkReferenceResolver = frameworkReferenceResolver;
            Dependencies = Enumerable.Empty<LibraryDescription>();
        }

        public IEnumerable<LibraryDescription> Dependencies { get; private set; }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return _projectResolver.SearchPaths.Select(p => Path.Combine(p, "{name}", "project.json"));
        }

        public LibraryDescription GetDescription(Library library, FrameworkName targetFramework)
        {
            if (library.IsGacOrFrameworkReference)
            {
                return null;
            }

            var name = library.Name;
            var version = library.Version;

            Project project;

            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            // This never returns null
            var targetFrameworkDependencies = project.GetTargetFramework(targetFramework).Dependencies;

            if (VersionUtility.IsDesktop(targetFramework))
            {
                targetFrameworkDependencies.Add(new Library
                {
                    Name = "mscorlib",
                    IsGacOrFrameworkReference = true,
                    IsImplicit = true
                });

                targetFrameworkDependencies.Add(new Library
                {
                    Name = "System",
                    IsGacOrFrameworkReference = true,
                    IsImplicit = true
                });

                targetFrameworkDependencies.Add(new Library
                {
                    Name = "System.Core",
                    IsGacOrFrameworkReference = true,
                    IsImplicit = true
                });

                targetFrameworkDependencies.Add(new Library
                {
                    Name = "Microsoft.CSharp",
                    IsGacOrFrameworkReference = true,
                    IsImplicit = true
                });
            }

            var dependencies = project.Dependencies.Concat(targetFrameworkDependencies).ToList();

            // TODO: Remove this code once there's a new build of the KRE
            // We need to keep this for bootstrapping to continue working
            foreach (var d in dependencies)
            {
                if (d.IsGacOrFrameworkReference)
                {
                    continue;
                }

                d.IsGacOrFrameworkReference = _frameworkReferenceResolver.TryGetAssembly(d.Name, targetFramework, out var path);

                // We need to fix up the version here since
                if (d.IsGacOrFrameworkReference)
                {
                    d.Version = VersionUtility.GetAssemblyVersion(path);
                }
            }

            return new LibraryDescription
            {
                Identity = new Library
                {
                    Name = project.Name,
                    Version = project.Version,
                    IsImplicit = library.IsImplicit
                },
                Dependencies = dependencies,
            };
        }

        public virtual void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework)
        {
            // PERF: It sucks that we have to do this twice. We should be able to round trip
            // the information from GetDescription
            foreach (var d in dependencies)
            {
                Project project;
                if (_projectResolver.TryResolveProject(d.Identity.Name, out project))
                {
                    d.Path = project.ProjectFilePath;
                    d.Type = "Project";
                }
            }

            Dependencies = dependencies;
        }
    }
}
