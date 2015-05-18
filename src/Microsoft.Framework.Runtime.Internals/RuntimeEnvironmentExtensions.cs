// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;

namespace Microsoft.Framework.Runtime
{
    internal static class RuntimeEnvironmentHelper
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

            return str.ToString();
        }

        public static string GetShortVersion(this IRuntimeEnvironment env)
        {
            return $"{env.RuntimeType}-{env.RuntimeArchitecture}-{env.RuntimeVersion}";
        }
    }
}
