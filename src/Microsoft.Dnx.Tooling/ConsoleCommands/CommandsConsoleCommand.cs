// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal class CommandsConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory, IApplicationEnvironment appEnvironment)
        {
            cmdApp.Command("commands", cmd =>
            {
                cmd.Description = "Commands related to managing application commands (install, uninstall)";
                cmd.HelpOption("-?|-h|--help");
                cmd.OnExecute(() =>
                {
                    cmd.ShowHelp();
                    return 2;
                });

                RegisterInstallSubcommand(cmd, reportsFactory, appEnvironment);
                RegisterUninstallSubcommand(cmd, reportsFactory);
            });
        }

        private static void RegisterInstallSubcommand(CommandLineApplication commandsCmd, ReportsFactory reportsFactory, IApplicationEnvironment appEnvironment)
        {
            commandsCmd.Command("install", c =>
            {
                c.Description = "Installs application commands";

                var argPackage = c.Argument("[package]", "The name of the application package");
                var argVersion = c.Argument("[version]", "The version of the application package");

                var optOverwrite = c.Option("-o|--overwrite", "Overwrites package and conflicting commands", CommandOptionType.NoValue);

                var feedCommandLineOptions = FeedCommandLineOptions.Add(c);

                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var feedOptions = feedCommandLineOptions.GetOptions();
                    var command = new InstallGlobalCommand(
                            appEnvironment,
                            string.IsNullOrEmpty(feedOptions.TargetPackagesFolder) ?
                                AppCommandsFolderRepository.CreateDefault() :
                                AppCommandsFolderRepository.Create(feedOptions.TargetPackagesFolder));

                    command.FeedOptions = feedOptions;
                    command.Reports = reportsFactory.CreateReports(feedOptions.Quiet);
                    command.OverwriteCommands = optOverwrite.HasValue();

                    if (feedOptions.Proxy != null)
                    {
                        Environment.SetEnvironmentVariable("http_proxy", feedOptions.Proxy);
                    }

                    if (argPackage.Value == null)
                    {
                        c.ShowHelp();
                        return 2;
                    }

                    var success = await command.Execute(argPackage.Value, argVersion.Value);
                    return success ? 0 : 1;
                });
            });
        }

        private static void RegisterUninstallSubcommand(CommandLineApplication commandsCmd, ReportsFactory reportsFactory)
        {
            commandsCmd.Command("uninstall", c =>
            {
                c.Description = "Uninstalls application commands";

                var argCommand = c.Argument("[command]", "The name of the command to uninstall");

                var optNoPurge = c.Option("--no-purge", "Do not try to remove orphaned packages", CommandOptionType.NoValue);

                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var command = new UninstallCommand(
                        AppCommandsFolderRepository.CreateDefault(),
                        reports: reportsFactory.CreateReports(quiet: false));

                    command.NoPurge = optNoPurge.HasValue();

                    var success = command.Execute(argCommand.Value);
                    return success ? 0 : 1;
                });
            });
        }
    }
}
