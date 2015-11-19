using System;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime
{
    public static class LockFileExtensions
    {
        public static bool HasTarget(this LockFile self, FrameworkName framework)
        {
            return self.Targets.Any(t => t.TargetFramework == framework && string.IsNullOrEmpty(t.RuntimeIdentifier));
        }

        public static bool HasTarget(this LockFile self, FrameworkName framework, string runtimeIdentifier)
        {
            return self.Targets.Any(t => t.TargetFramework == framework && string.Equals(t.RuntimeIdentifier, runtimeIdentifier, StringComparison.Ordinal));
        }
    }
}
