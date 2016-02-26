// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dnx.Runtime
{
    public class LockFile
    {
        public bool Islocked { get; set; }
        public int Version { get; set; }
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

                // HACK(anurse): Support the slightly-different NuGet-style lock file ordering/formatting as a fallback
                IOrderedEnumerable<string> nugetStyleDependencies;
                var expectedDependencies = group.Dependencies
                    .Select(TrimFx)
                    .OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (string.IsNullOrEmpty(group.FrameworkName))
                {
                    actualDependencies = project.Dependencies
                        .Select(d => TrimFx(d.LibraryRange.ToString()))
                        .OrderBy(x => x, StringComparer.Ordinal);
                    nugetStyleDependencies = project.Dependencies
                        .Select(d => TrimFx(d.LibraryRange.ToString()))
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
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

                    actualDependencies = framework.Dependencies
                        .Select(d => TrimFx(d.LibraryRange.ToString()))
                        .OrderBy(x => x, StringComparer.Ordinal);
                    nugetStyleDependencies = framework.Dependencies
                        .Select(d => TrimFx(d.LibraryRange.ToString()))
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies) &&
                    !nugetStyleDependencies.SequenceEqual(expectedDependencies.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            message = null;
            return true;
        }

        private string TrimFx(string s)
        {
            var prefix = "fx/";
            if (s.StartsWith(prefix))
            {
                return s.Substring(prefix.Length);
            }
            return s;
        }
    }
}