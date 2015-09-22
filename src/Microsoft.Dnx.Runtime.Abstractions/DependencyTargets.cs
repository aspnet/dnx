// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Runtime
{
    public static class DependencyTargets
    {
        public static readonly string Project = nameof(Project);
        public static readonly string Package = nameof(Package);

        private const string NoTargetDisplayName = "Dependency";

        public static string GetDisplayForTarget(string target)
        {
            // Normalize the case of the known targets
            if (string.Equals(target, Project, StringComparison.OrdinalIgnoreCase))
            {
                return Project;
            }
            else if (string.Equals(target, Package, StringComparison.OrdinalIgnoreCase))
            {
                return Package;
            }
            else
            {
                return NoTargetDisplayName;
            }
        }

        public static bool SupportsPackage(string target)
        {
            return string.IsNullOrEmpty(target) || string.Equals(target, Package, StringComparison.OrdinalIgnoreCase);
        }

        public static bool SupportsProject(string target)
        {
            return string.IsNullOrEmpty(target) || string.Equals(target, Project, StringComparison.OrdinalIgnoreCase);
        }
    }
}
