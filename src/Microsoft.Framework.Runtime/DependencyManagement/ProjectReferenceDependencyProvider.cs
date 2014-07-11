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

        public ProjectReferenceDependencyProvider(IProjectResolver projectResolver)
        {
            _projectResolver = projectResolver;
            Dependencies = Enumerable.Empty<LibraryDescription>();
        }

        public IEnumerable<LibraryDescription> Dependencies { get; private set; }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return _projectResolver.SearchPaths.Select(p => Path.Combine(p, "{name}", "project.json"));
        }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
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
                // mscorlib is ok
                targetFrameworkDependencies.Add(new Library { Name = "mscorlib" });

                // TODO: Remove these references (but we need to update the dependent projects first)
                targetFrameworkDependencies.Add(new Library { Name = "System" });
                targetFrameworkDependencies.Add(new Library { Name = "System.Core" });
                targetFrameworkDependencies.Add(new Library { Name = "Microsoft.CSharp" });
            }

            return new LibraryDescription
            {
                Identity = new Library { Name = project.Name, Version = project.Version },
                Dependencies = project.Dependencies.Concat(targetFrameworkDependencies),
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
