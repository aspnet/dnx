// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
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
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } =
            new List<ProjectFileDependencyGroup>();
        public IList<LockFileLibrary> Libraries { get; set; } = new List<LockFileLibrary>();

        public bool IsValidForProject(Project project)
        {
            if (Version != LockFileFormat.Version)
            {
                return false;
            }

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
                            string.Equals(f.FrameworkName.ToString(), group.FrameworkName, StringComparison.OrdinalIgnoreCase));
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

            return true;
        }
    }
}