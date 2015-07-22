// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal static class WrapConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory)
        {
            cmdApp.Command("wrap", c =>
            {
                c.Description = "Wrap a csproj/assembly into a project.json, which can be referenced by project.json files";

                var argPath = c.Argument("[path]", "Path to csproj/assembly to be wrapped");
                var optConfiguration = c.Option("--configuration <CONFIGURATION>",
                    "Configuration of wrapped project, default is 'debug'", CommandOptionType.SingleValue);
                var optMsBuildPath = c.Option("--msbuild <PATH>",
                    @"Path to MSBuild, default is '%ProgramFiles%\MSBuild\14.0\Bin\MSBuild.exe'",
                    CommandOptionType.SingleValue);
                var optInPlace = c.Option("-i|--in-place",
                    "Generate or update project.json files in project directories of csprojs",
                    CommandOptionType.NoValue);
                var optFramework = c.Option("-f|--framework",
                    "Target framework of assembly to be wrapped",
                    CommandOptionType.SingleValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var reports = reportsFactory.CreateReports(quiet: false);

                    var command = new WrapCommand();
                    command.Reports = reports;
                    command.InputFilePath = argPath.Value;
                    command.Configuration = optConfiguration.Value();
                    command.MsBuildPath = optMsBuildPath.Value();
                    command.InPlace = optInPlace.HasValue();
                    command.Framework = optFramework.Value();

                    var success = command.Execute();

                    return success ? 0 : 1;
                });
            });
        }

    }
}
