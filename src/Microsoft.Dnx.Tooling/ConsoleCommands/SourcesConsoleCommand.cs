// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Tooling.SourceControl;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal class SourcesConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory)
        {
            cmdApp.Command("sources", cmd =>
            {
                cmd.Description = "Commands related managing packages source code from external repositories.";
                cmd.HelpOption("-?|-h|--help");
                cmd.OnExecute(() =>
                {
                    cmd.ShowHelp();
                    return 2;
                });

                RegisterGetSubcommand(cmd, reportsFactory);
            });
        }

        private static void RegisterGetSubcommand(CommandLineApplication sourcesCmd, ReportsFactory reportsFactory)
        {
            sourcesCmd.Command("get", c =>
            {
                c.Description = "Retrieves the source code for packages used by projects";

                var packagesArgument = c.Argument(
                    "[package]",
                    "The name of the package for which to retrieve the sources. Can only specify packages used by the project.",
                    multipleValues: true);

                var projectFileOption = c.Option(
                    "-p|--project",
                    "Optional. The path to a project.json file. If not specified, the project in the current folder is used.",
                    CommandOptionType.SingleValue);
                var packagesFolderOption = c.Option(
                    "--packages",
                    "Optional. The local packages folder",
                    CommandOptionType.SingleValue);
                var sourceFolderOption = c.Option(
                    "-o|--output",
                    "Optional. The path to the folder that will hold the source files.",
                    CommandOptionType.SingleValue);

                c.OnExecute(() =>
                {
                    var command = new SourceCommand(packagesArgument.Value, reportsFactory.CreateReports(quiet: false));
                    command.ProjectFile = projectFileOption.Value();
                    command.PackagesFolder = packagesFolderOption.Value();
                    command.SourcesFolder = sourceFolderOption.Value();

                    if (!command.Execute())
                    {
                        return -1;
                    }

                    return 0;
                });
            });
        }
    }
}
