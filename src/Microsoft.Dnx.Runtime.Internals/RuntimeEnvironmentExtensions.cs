// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime
{
    internal static class RuntimeEnvironmentExtensions
    {
        // This code doesn't have access to EnvironmentNames and I don't want to shake things up too much
        // This should be the only place it is used though so it should be fine
        private const string DnxRuntimeIdEnvironmentVariable = "DNX_RUNTIME_ID";

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
            var overrideRid = GetOverrideRid();
            return string.IsNullOrEmpty(overrideRid) ?
                GetRuntimeIdentifierWithoutOverride(env) :
                overrideRid;
        }

        // This is intentionally NOT an extension method because it is only really here for testing
        internal static string GetRuntimeIdentifierWithoutOverride(IRuntimeEnvironment env)
        {
            return $"{GetRuntimeOsName(env)}-{env.RuntimeArchitecture.ToLower()}";
        }

        public static IEnumerable<string> GetAllRuntimeIdentifiers(this IRuntimeEnvironment env)
        {
            var overrideRid = GetOverrideRid();
            return string.IsNullOrEmpty(overrideRid) ?
                GetAllRuntimeIdentifiersWithoutOverride(env) :
                new [] { overrideRid };
        }

        // This is intentionally NOT an extension method because it is only really here for testing
        internal static IEnumerable<string> GetAllRuntimeIdentifiersWithoutOverride(IRuntimeEnvironment env)
        {
            if (!string.Equals(env.OperatingSystem, RuntimeOperatingSystems.Windows, StringComparison.Ordinal))
            {
                yield return GetRuntimeIdentifierWithoutOverride(env);
            }
            else
            {
                var arch = env.RuntimeArchitecture.ToLowerInvariant();
                if (env.OperatingSystemVersion.Equals("6.1", StringComparison.Ordinal))
                {
                    yield return "win7-" + arch;
                }
                else if (env.OperatingSystemVersion.Equals("6.2", StringComparison.Ordinal))
                {
                    yield return "win8-" + arch;
                    yield return "win7-" + arch;
                }
                else if (env.OperatingSystemVersion.Equals("6.3", StringComparison.Ordinal))
                {
                    yield return "win81-" + arch;
                    yield return "win8-" + arch;
                    yield return "win7-" + arch;
                }
                else if (env.OperatingSystemVersion.Equals("10.0", StringComparison.Ordinal))
                {
                    yield return "win10-" + arch;
                    yield return "win81-" + arch;
                    yield return "win8-" + arch;
                    yield return "win7-" + arch;
                }
            }
        }

        public static string GetRuntimeOsName(this IRuntimeEnvironment env)
        {
            string os = env.OperatingSystem ?? string.Empty;
            string ver = env.OperatingSystemVersion ?? string.Empty;
            if (string.Equals(os, RuntimeOperatingSystems.Windows, StringComparison.Ordinal))
            {
                os = "win";

                if (env.OperatingSystemVersion.Equals("6.1", StringComparison.Ordinal))
                {
                    ver = "7";
                }
                else if (env.OperatingSystemVersion.Equals("6.2", StringComparison.Ordinal))
                {
                    ver = "8";
                }
                else if (env.OperatingSystemVersion.Equals("6.3", StringComparison.Ordinal))
                {
                    ver = "81";
                }
                else if (env.OperatingSystemVersion.Equals("10.0", StringComparison.Ordinal))
                {
                    ver = "10";
                }

                return os + ver;
            }
            else if (string.Equals(os, RuntimeOperatingSystems.Linux, StringComparison.Ordinal))
            {
                // Generate the distro-based name
                var split = ver.Split(new[] { ' ' }, 2);
                if (split.Length == 2)
                {
                    os = split[0].ToLowerInvariant();
                    ver = split[1].ToLowerInvariant();
                }
                else
                {
                    os = "linux";
                    ver = string.Empty;
                }
            }
            else if (string.Equals(os, RuntimeOperatingSystems.Darwin, StringComparison.Ordinal))
            {
                os = "osx";
            }
            else
            {
                // Just use the lower-case full name of the OS as the RID OS and tack on the version number
                os = os.ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(ver))
            {
                os = os + "." + ver;
            }
            return os;
        }

        private static string GetOverrideRid()
        {
            return Environment.GetEnvironmentVariable(DnxRuntimeIdEnvironmentVariable);
        }
    }
}
