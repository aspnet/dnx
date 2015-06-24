// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace Microsoft.Framework.PackageManager
{
    internal class ListSourcesCommand
    {
        public Reports Reports { get; }
        public string TargetDirectory { get; }

        public ListSourcesCommand(Reports reports, string targetDirectory)
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
                Reports.Information.WriteLine("Registered Sources:");

                // Iterate over the sources and report them
                int index = 1; // This is an index for display so it should be 1-based
                foreach (var source in sources)
                {
                    var enabledString = source.IsEnabled ? "" : " [Disabled]";
                    var line = $"{index.ToString("0\\.").PadRight(3)} {source.Name} {source.Source}{enabledString.Yellow().Bold()}";
                    Reports.Information.WriteLine(line);

                    index++;
                }
            }
            return 0;
        }
    }
}
