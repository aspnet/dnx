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

            return null;
        }
    }
}
