// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting.DependencyProviders
{
    public class ProjectReferenceDependencyProvider : IDependencyProvider
    {
        private readonly IProjectResolver _projectResolver;

        public ProjectReferenceDependencyProvider(IProjectResolver projectResolver)
        {
            _projectResolver = projectResolver;
        }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return _projectResolver.SearchPaths.Select(p => Path.Combine(p, "{name}", "project.json"));
        }

        public bool SupportsType(string libraryType)
        {
            return string.IsNullOrEmpty(libraryType) ||
                   string.Equals(libraryType, LibraryTypes.Project);
        }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            string name = libraryRange.Name;

            Project project;

            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            // This never returns null
            var targetFrameworkInfo = project.Metadata.GetTargetFramework(targetFramework);
            var targetFrameworkDependencies = targetFrameworkInfo.Dependencies;

            if (targetFramework.IsDesktop())
            {
                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "mscorlib",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System.Core",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "Microsoft.CSharp",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });
            }

            var dependencies = project.Metadata.Dependencies.Concat(targetFrameworkDependencies).ToList();

            // Mark the library as unresolved if there were specified frameworks
            // and none of them resolved
            bool unresolved = targetFrameworkInfo.FrameworkName == null &&
                              project.Metadata.TargetFrameworks.Any();

            var description = new Library
            {
                LibraryRange = libraryRange,
                Identity = new LibraryIdentity
                {
                    Name = project.Name,
                    Version = project.Version,
                    Type = LibraryTypes.Project,
                },
                Path = project.FilePath,
                Dependencies = dependencies,
                Resolved = !unresolved
            };

            description.Items["project"] = project;

            return description;
        }
    }
}
