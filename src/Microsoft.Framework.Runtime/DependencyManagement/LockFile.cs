// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class LockFile
    {
        public bool Islocked { get; set; }
        public int Version { get; set; }
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } = new List<ProjectFileDependencyGroup>();
        public IList<LockFileLibrary> Libraries { get; set; } = new List<LockFileLibrary>();
        public IList<LockFileTarget> Targets { get; set; } = new List<LockFileTarget>();

        public bool IsValidForProject(Project project, out string message)
        {
            message = null;

            if (Version != LockFileReader.Version)
            {
                message = $"The expected lock file version ({LockFileReader.Version}) does not match the actual version ({Version}).";
                return false;
            }

            var actualTargetFrameworks = project.GetTargetFrameworks();

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            var frameworkNumDiff = ProjectFileDependencyGroups.Count - (actualTargetFrameworks.Count() + 1);
            if (frameworkNumDiff != 0)
            {
                var action = frameworkNumDiff < 0 ? "added to" : "removed from";
                message = $"One or more frameworks were {action} {Project.ProjectFileName}";
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
                        message = $"The framework '{group.FrameworkName}' was removed from {Project.ProjectFileName}";
                        return false;
                    }

                    actualDependencies = framework.Dependencies.Select(d => d.LibraryRange.ToString()).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    if (string.IsNullOrEmpty(group.FrameworkName))
                    {
                        message = $"Shared dependencies in {Project.ProjectFileName} were modified";
                    }
                    else
                    {
                        message = $"Dependencies of framework '{group.FrameworkName}' in {Project.ProjectFileName} were modified";
                    }

                    return false;
                }
            }

            return true;
        }
    }
}