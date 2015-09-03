// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public static class ProjectExtensions
    {
        public static TargetFrameworkInformation GetCompatibleTargetFramework(this Runtime.Project project, FrameworkName targetFramework)
        {
            IEnumerable<TargetFrameworkInformation> targets;
            if (VersionUtility.GetNearest(targetFramework, project.GetTargetFrameworks(), out targets) &&
                targets.Any())
            {
                return targets.FirstOrDefault();
            }

            return new TargetFrameworkInformation
            {
                Dependencies = new List<LibraryDependency>()
            };
        }
    }
}
