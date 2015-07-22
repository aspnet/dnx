// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal class PackConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory, IServiceProvider serviceProvider)
        {
            cmdApp.Command("pack", c =>
            {
                c.Description = "Build NuGet packages for the project in given directory";

                var optionFramework = c.Option("--framework <TARGET_FRAMEWORK>", "A list of target frameworks to build.", CommandOptionType.MultipleValue);
                var optionConfiguration = c.Option("--configuration <CONFIGURATION>", "A list of configurations to build.", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTPUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionQuiet = c.Option("--quiet", "Do not show output such as source/destination of nupkgs",
                    CommandOptionType.NoValue);
                var argProjectDir = c.Argument(
                    "[projects]",
                    "One or more projects to pack, default is current directory",
                    multipleValues: true);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var buildOptions = new BuildOptions();
                    buildOptions.OutputDir = optionOut.Value();
                    buildOptions.ProjectPatterns = argProjectDir.Values;
                    if (buildOptions.ProjectPatterns.Count == 0)
                    {
                        buildOptions.ProjectPatterns.Add(Path.Combine(Directory.GetCurrentDirectory(), "project.json"));
                    }
                    buildOptions.Configurations = optionConfiguration.Values;
                    buildOptions.TargetFrameworks = optionFramework.Values;
                    buildOptions.GeneratePackages = true;
                    buildOptions.Reports = reportsFactory.CreateReports(optionQuiet.HasValue());

                    var projectManager = new BuildManager(serviceProvider, buildOptions);

                    if (!projectManager.Build())
                    {
                        return -1;
                    }

                    return 0;
                });
            });
        }
    }
}
