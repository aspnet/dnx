// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            var targetFrameworkInfo = project.GetTargetFramework(targetFramework);
            var targetFrameworkDependencies = targetFrameworkInfo.Dependencies;

            if (VersionUtility.IsDesktop(targetFramework))
            {
                targetFrameworkDependencies.Add(new LibraryDependency(
                    name: "mscorlib",
                    isGacOrFrameworkReference: true));

                targetFrameworkDependencies.Add(new LibraryDependency(
                    name: "System",
                    isGacOrFrameworkReference: true));

                targetFrameworkDependencies.Add(new LibraryDependency(
                    name: "System.Core",
                    isGacOrFrameworkReference: true));

                targetFrameworkDependencies.Add(new LibraryDependency(
                    name: "Microsoft.CSharp",
                    isGacOrFrameworkReference: true));
            }

            var dependencies = project.Dependencies.Concat(targetFrameworkDependencies).ToList();

            return new LibraryDescription
            {
                Identity = new Library
                {
                    Name = project.Name,
                    Version = project.Version
                },
                Type = "Project",
                Path = project.ProjectFilePath,
                Framework = targetFrameworkInfo.FrameworkName,
                Dependencies = dependencies,
            };
        }

        public virtual void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework)
        {
            Dependencies = dependencies;
        }
    }
}
