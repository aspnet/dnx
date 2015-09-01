// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Dnx.Runtime
{
    internal static class RuntimeEnvironmentExtensions
    {
        public static string GetFullVersion(this IRuntimeEnvironment env)
        {
            var str = new StringBuilder();
            str.AppendLine($" Version:      {env.RuntimeVersion}");
            str.AppendLine($" Type:         {env.RuntimeType}");
            str.AppendLine($" Architecture: {env.RuntimeArchitecture}");
            str.AppendLine($" OS Name:      {env.OperatingSystem}");

            if (!string.IsNullOrEmpty(env.OperatingSystemVersion))
            {
                str.AppendLine($" OS Version:   {env.OperatingSystemVersion}");
            }

            str.AppendLine($" Runtime Id:   {env.GetRuntimeIdentifier()}");

            return str.ToString();
        }

        public static string GetShortVersion(this IRuntimeEnvironment env)
        {
            return $"{env.RuntimeType}-{env.RuntimeArchitecture}-{env.RuntimeVersion}";
        }

        public static string GetRuntimeIdentifier(this IRuntimeEnvironment env)
        {
            return $"{GetRuntimeOsName(env)}-{env.RuntimeArchitecture.ToLower()}";
        }

        public static IEnumerable<string> GetAllRuntimeIdentifiers(this IRuntimeEnvironment env)
        {
            if (!string.Equals(env.OperatingSystem, RuntimeOperatingSystems.Windows, StringComparison.Ordinal))
            {
                yield return env.GetRuntimeIdentifier();
            }
            else
            {
                var arch = env.RuntimeArchitecture.ToLowerInvariant();
                var parsedVersion = Version.Parse(env.OperatingSystemVersion);
                if (parsedVersion.Major == 6 && parsedVersion.Minor == 1)
                {
                    yield return "win7-" + arch;
                }
                else if (parsedVersion.Major == 6 && parsedVersion.Minor == 2)
                {
                    yield return "win8-" + arch;
                    yield return "win7-" + arch;
                }
                else if (parsedVersion.Major == 6 && parsedVersion.Minor == 3)
                {
                    yield return "win81-" + arch;
                    yield return "win8-" + arch;
                    yield return "win7-" + arch;
                }
                else if (parsedVersion.Major == 10 && parsedVersion.Minor == 0)
                {
                    yield return "win10-" + arch;
                    yield return "win81-" + arch;
                    yield return "win8-" + arch;
                    yield return "win7-" + arch;
                }
            }
        }

        private static string GetRuntimeOsName(this IRuntimeEnvironment env)
        {
            string os = env.OperatingSystem;
            string ver = env.OperatingSystemVersion;
            if (string.Equals(os, RuntimeOperatingSystems.Windows, StringComparison.Ordinal))
            {
                os = "win";

                // Convert 6.x to the correct branding version for the RID
                var parsedVersion = Version.Parse(ver);
                if (parsedVersion.Major == 6 && parsedVersion.Minor == 1)
                {
                    ver = "7";
                }
                else if (parsedVersion.Major == 6 && parsedVersion.Minor == 2)
                {
                    ver = "8";
                }
                else if (parsedVersion.Major == 6 && parsedVersion.Minor == 3)
                {
                    ver = "81";
                }
                else if (parsedVersion.Major == 10 && parsedVersion.Minor == 0)
                {
                    ver = "10";
                }
            }
            else
            {
                os = os.ToLowerInvariant(); // Just use the lower-case full name of the OS as the RID OS
            }
            return os + ver;
        }
    }
}
