// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    /// <summary>
    /// Manages the NuGet.config hierarchy
    /// </summary>
    public class NuGetConfig
    {
        public NuGetConfig(ISettings settings, PackageSourceProvider sources)
        {
            Settings = settings;
            Sources = sources;
        }

        public ISettings Settings { get; }

        public IPackageSourceProvider Sources { get; }

        public static NuGetConfig ForSolution(string solutionDirectory)
        {
            return ForSolution(solutionDirectory, new PhysicalFileSystem(Directory.GetCurrentDirectory()));
        }

        public static NuGetConfig ForSolution(string solutionDirectory, IFileSystem fileSystem)
        {
            var settings = SettingsUtils.ReadSettings(
                solutionDirectory, 
                nugetConfigFile: null, 
                fileSystem: fileSystem, 
                machineWideSettings: new CommandLineMachineWideSettings());

            // Recreate the source provider
            var sources = PackageSourceBuilder.CreateSourceProvider(settings);

            return new NuGetConfig(settings, sources);
        }
    }
}
