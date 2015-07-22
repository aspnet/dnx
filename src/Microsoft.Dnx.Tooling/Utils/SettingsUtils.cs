// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public static class SettingsUtils
    {
        public static ISettings ReadSettings(string solutionDir, string nugetConfigFile, IFileSystem fileSystem,
            IMachineWideSettings machineWideSettings)
        {
            // Read the solution-level settings
            var solutionSettingsFile = Path.Combine(solutionDir, NuGetConstants.NuGetSolutionSettingsFolder);
            var fullPath = fileSystem.GetFullPath(solutionSettingsFile);
            var solutionSettingsFileSystem = new PhysicalFileSystem(fullPath);

            if (nugetConfigFile != null)
            {
                nugetConfigFile = fileSystem.GetFullPath(nugetConfigFile);
            }

            var settings = Settings.LoadDefaultSettings(
                fileSystem: solutionSettingsFileSystem,
                configFileName: nugetConfigFile,
                machineWideSettings: machineWideSettings);

            return settings;
        }
    }
}