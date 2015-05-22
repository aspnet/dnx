// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager
{
    internal class BuildConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory, IServiceProvider serviceProvider)
        {
            cmdApp.Command("build", c =>
            {
                c.Description = "Produce assemblies for the project in given directory";

                var optionFramework = c.Option("--framework <TARGET_FRAMEWORK>", "A list of target frameworks to build.", CommandOptionType.MultipleValue);
                var optionConfiguration = c.Option("--configuration <CONFIGURATION>", "A list of configurations to build.", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTPUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionQuiet = c.Option("--quiet", "Do not show output such as dependencies in use",
                    CommandOptionType.NoValue);
                var argProjectDir = c.Argument("[project]", "Project to build, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var buildOptions = new BuildOptions();
                    buildOptions.OutputDir = optionOut.Value();
                    buildOptions.ProjectDir = argProjectDir.Value ?? Directory.GetCurrentDirectory();
                    buildOptions.Configurations = optionConfiguration.Values;
                    buildOptions.TargetFrameworks = optionFramework.Values;
                    buildOptions.GeneratePackages = false;
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
