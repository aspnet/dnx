// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    internal class ListFeedsCommand
    {
        public Reports Reports { get; }
        public string TargetDirectory { get; }

        public ListFeedsCommand(Reports reports, string targetDirectory)
        {
            Reports = reports;
            TargetDirectory = targetDirectory;
        }

        public int Execute()
        {
            // Most of this code is from NuGet, just some refactoring for our system.
            // Collect config
            var config = NuGetConfig.ForSolution(TargetDirectory);

            var sources = config.Sources.LoadPackageSources();
            if (!sources.Any())
            {
                Reports.Information.WriteLine("No sources found.");
            }
            else
            {
                Reports.Information.WriteLine("Feeds in use:");

                // Iterate over the sources and report them
                foreach (var source in sources)
                {
                    var enabledString = source.IsEnabled ? "" : " [Disabled]";

                    var line = $"    {source.Source}{enabledString.Yellow().Bold()}";
                    Reports.Information.WriteLine(line);

                    var origin = source.Origin as Settings;
                    if (origin != null)
                    {
                        Reports.Information.WriteLine($"      Origin: {origin.ConfigFilePath}");
                    }
                    else if (source.IsOfficial)
                    {
                        Reports.Information.WriteLine($"      Offical NuGet Source, enabled by default");
                    }
                }
            }

            // Display config files in use
            var settings = config.Settings as Settings;
            if (settings != null)
            {
                var configFiles = settings.GetConfigFiles();

                Reports.Quiet.WriteLine($"{Environment.NewLine}NuGet Config files in use:");
                foreach (var file in configFiles)
                {
                    Reports.Quiet.WriteLine($"    {file}");
                }
            }
            return 0;
        }
    }
}
