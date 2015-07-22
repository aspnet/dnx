// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Tooling.Packages;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal static class PackagesConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory)
        {
            cmdApp.Command("packages", packagesCommand =>
            {
                packagesCommand.Description = "Commands related to managing local and remote packages folders";
                packagesCommand.HelpOption("-?|-h|--help");
                packagesCommand.OnExecute(() =>
                {
                    packagesCommand.ShowHelp();
                    return 2;
                });

                RegisterAddSubcommand(packagesCommand, reportsFactory);
                RegisterPushSubcommand(packagesCommand, reportsFactory);
                RegisterPullSubcommand(packagesCommand, reportsFactory);
            });
        }

        private static void RegisterAddSubcommand(CommandLineApplication packagesCmd, ReportsFactory reportsFactory)
        {
            packagesCmd.Command("add", c =>
            {
                c.Description = "Add a NuGet package to the specified packages folder";
                var argNupkg = c.Argument("[nupkg]", "Path to a NuGet package");
                var argSource = c.Argument("[source]", "Path to packages folder");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var options = new AddOptions
                    {
                        Reports = reportsFactory.CreateReports(quiet: false),
                        SourcePackages = argSource.Value,
                        NuGetPackage = argNupkg.Value
                    };
                    var command = new Packages.AddCommand(options);
                    var success = await command.Execute();
                    return success ? 0 : 1;
                });
            });
        }

        private static void RegisterPushSubcommand(CommandLineApplication packagesCmd, ReportsFactory reportsFactory)
        {
            packagesCmd.Command("push", c =>
            {
                c.Description = "Incremental copy of files from local packages to remote location";
                var argRemote = c.Argument("[remote]", "Path to remote packages folder");
                var argSource = c.Argument("[source]",
                    "Path to source packages folder, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var reports = reportsFactory.CreateReports(quiet: false);

                    // Implicitly commit changes before push
                    var commitOptions = new CommitOptions
                    {
                        Reports = reports,
                        SourcePackages = argSource.Value
                    };
                    var commitCommand = new CommitCommand(commitOptions);
                    var success = commitCommand.Execute();
                    if (!success)
                    {
                        return 1;
                    }

                    var pushOptions = new PushOptions
                    {
                        Reports = reports,
                        SourcePackages = argSource.Value,
                        RemotePackages = argRemote.Value
                    };
                    var pushCommand = new PushCommand(pushOptions);
                    success = pushCommand.Execute();
                    return success ? 0 : 1;
                });
            });
        }

        private static void RegisterPullSubcommand(CommandLineApplication packagesCmd, ReportsFactory reportsFactory)
        {
            packagesCmd.Command("pull", c =>
            {
                c.Description = "Incremental copy of files from remote location to local packages";
                var argRemote = c.Argument("[remote]", "Path to remote packages folder");
                var argSource = c.Argument("[source]",
                    "Path to source packages folder, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var reports = reportsFactory.CreateReports(quiet: false);

                    bool success;
                    if (Directory.Exists(argSource.Value))
                    {
                        // Implicitly commit changes before pull
                        var commitOptions = new CommitOptions
                        {
                            Reports = reports,
                            SourcePackages = argSource.Value
                        };
                        var commitCommand = new CommitCommand(commitOptions);
                        success = commitCommand.Execute();
                        if (!success)
                        {
                            return 1;
                        }
                    }

                    var pullOptions = new PullOptions
                    {
                        Reports = reports,
                        SourcePackages = argSource.Value,
                        RemotePackages = argRemote.Value
                    };
                    var pullCommand = new PullCommand(pullOptions);
                    success = pullCommand.Execute();
                    return success ? 0 : 1;
                });
            });
        }
    }
}
