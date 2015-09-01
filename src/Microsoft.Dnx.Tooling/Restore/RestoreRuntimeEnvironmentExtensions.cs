using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling
{
    public static class RestoreRuntimeEnvironmentExtensions
    {
        public static IEnumerable<string> GetDefaultRestoreRuntimes(this IRuntimeEnvironment env)
        {
            if (string.Equals(env.OperatingSystem, RuntimeOperatingSystems.Windows, StringComparison.Ordinal))
            {
                // Restore the minimum version of Windows. If the user wants other runtimes, they need to opt-in
                yield return "win7-x86";
                yield return "win7-x64";
            }
            else
            {
                var os = env.OperatingSystem.ToLowerInvariant();
                yield return os + "-x86"; // We do support x86 on Linux/Darwin via Mono
                yield return os + "-x64";
            }
        }

    }
}
