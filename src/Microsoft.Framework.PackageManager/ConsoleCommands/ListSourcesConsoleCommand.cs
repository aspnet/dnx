﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager
{
    internal static class FeedsConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory)
        {
            cmdApp.Command("feeds", c =>
            {
                c.Description = "Commands related to managing package feeds currently in use";
                c.HelpOption("-?|-h|--help");
                c.OnExecute(() =>
                {
                    c.ShowHelp();
                    return 2;
                });

                RegisterListCommand(c, reportsFactory);
            });
        }

        public static void RegisterListCommand(CommandLineApplication cmdApp, ReportsFactory reportsFactory)
        {
            cmdApp.Command("list", c =>
            {
                c.Description = "Displays a list of package sources in effect for a project";
                var argRoot = c.Argument("[root]",
                    "The path of the project to calculate effective package sources for (defaults to the current directory)");

                c.OnExecute(() =>
                {
                    var command = new ListSourcesCommand(
                        reportsFactory.CreateReports(quiet: false),
                        string.IsNullOrEmpty(argRoot.Value) ? "." : argRoot.Value);

                    return command.Execute();
                });
            });
        }
    }
}
