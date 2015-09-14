// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dnx.Runtime
{
    public class LockFile
    {
        public bool Islocked { get; set; }
        public int Version { get; set; }
        public IList<string> GlobalSearchPaths { get; set; } = new List<string>();
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } = new List<ProjectFileDependencyGroup>();
        public IList<LockFilePackageLibrary> PackageLibraries { get; set; } = new List<LockFilePackageLibrary>();
        public IList<LockFileProjectLibrary> ProjectLibraries { get; set; } = new List<LockFileProjectLibrary>();
        public IList<LockFileTarget> Targets { get; set; } = new List<LockFileTarget>();

        public bool IsValidForProject(Project project)
        {
            string message;
            return IsValidForProject(project, out message);
        }

        public bool IsValidForProject(Project project, out string message)
        {
            if (Version != Constants.LockFileVersion)
            {
                message = $"The expected lock file version does not match the actual version";
                return false;
            }

            // Verify the global search paths
            message = $"The project search paths defined in {GlobalSettings.GlobalFileName} have changed";
            GlobalSettings globalSettings;
            if (GlobalSettings.TryGetGlobalSettings(ProjectRootResolver.ResolveRootDirectory(project.ProjectDirectory), out globalSettings))
            {
                if (globalSettings.ProjectSearchPaths.Count != GlobalSearchPaths.Count)
                {
                    return false;
                }

                // The search paths were saved in lock file orderedly
                var orderedPaths = globalSettings.ProjectSearchPaths.OrderBy(x => x).ToList();

                for (int i = 0; i < globalSettings.ProjectSearchPaths.Count; ++i)
                {
                    if (!string.Equals(orderedPaths[i], GlobalSearchPaths[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (GlobalSearchPaths.Any())
                {
                    return false;
                }
            }

            message = $"Dependencies in {Project.ProjectFileName} were modified";

            var actualTargetFrameworks = project.GetTargetFrameworks();

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                return false;
            }

            foreach (var group in ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = group.Dependencies.OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (string.IsNullOrEmpty(group.FrameworkName))
                {
                    actualDependencies = project.Dependencies.Select(x => x.LibraryRange.ToString()).OrderBy(x => x);
                }
                else
                {
                    var framework = actualTargetFrameworks
                        .FirstOrDefault(f =>
                            string.Equals(f.FrameworkName.ToString(), group.FrameworkName, StringComparison.Ordinal));
                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework.Dependencies.Select(d => d.LibraryRange.ToString()).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }

            message = null;
            return true;
        }
    }
}