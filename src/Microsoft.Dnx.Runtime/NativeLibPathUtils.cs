// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Dnx.Runtime
{
    internal class NativeLibPathUtils
    {
        public static IEnumerable<string> GetNativeSubfolderCandidates(IRuntimeEnvironment runtimeEnvironment)
        {
            if (runtimeEnvironment.OperatingSystem == RuntimeOperatingSystems.Windows)
            {
                return PlatformServices.Default.Runtime.GetAllRuntimeIdentifiers();
            }

            var runtimeId = PlatformServices.Default.Runtime.GetRuntimeOsName();

            return new[]
            {
                runtimeId,
                runtimeId.Split('-')[0],
                runtimeId.Split('.')[0]
            };
        }

        public static string GetProjectNativeLibPath(string projectPath, string nativeSubfolder)
        {
            return Path.Combine(projectPath, "runtimes", nativeSubfolder, "native");
        }

        public static bool IsMatchingNativeLibrary(IRuntimeEnvironment runtimeEnvironment, string requestedFile, string actualFile)
        {
            if (string.Equals(requestedFile, actualFile, StringComparison.Ordinal))
            {
                return true;
            }

            if (runtimeEnvironment.OperatingSystem == RuntimeOperatingSystems.Windows)
            {
                return string.Equals(requestedFile, actualFile, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(requestedFile + ".dll", actualFile, StringComparison.OrdinalIgnoreCase);
            }

            if (runtimeEnvironment.OperatingSystem == RuntimeOperatingSystems.Linux)
            {
                return string.Equals(requestedFile + ".so", actualFile, StringComparison.Ordinal);
            }

            return string.Equals(requestedFile + ".dylib", actualFile, StringComparison.Ordinal);
        }

    }
}
