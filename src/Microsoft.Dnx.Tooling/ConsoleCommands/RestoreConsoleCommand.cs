// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal static class RestoreConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory, IApplicationEnvironment applicationEnvironment)
        {
            cmdApp.Command("restore", c =>
            {
                c.Description = "Restore packages";

                var argRoot = c.Argument("[root]",
                    "List of projects and project folders to restore. Each value can be: a path to a project.json or global.json file, or a folder to recursively search for project.json files.",
                    multipleValues: true);
                var feedCommandLineOptions = FeedCommandLineOptions.Add(c);
                var optLock = c.Option("--lock",
                    "Creates dependencies file with locked property set to true. Overwrites file if it exists.",
                    CommandOptionType.NoValue);
                var optUnlock = c.Option("--unlock",
                    "Creates dependencies file with locked property set to false. Overwrites file if it exists.",
                    CommandOptionType.NoValue);

                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var feedOptions = feedCommandLineOptions.GetOptions();
                    var command = new RestoreCommand(applicationEnvironment);
                    command.Reports = reportsFactory.CreateReports(feedOptions.Quiet);
                    command.RestoreDirectories.AddRange(argRoot.Values);
                    command.FeedOptions = feedOptions;
                    command.Lock = optLock.HasValue();
                    command.Unlock = optUnlock.HasValue();

                    if (!string.IsNullOrEmpty(feedOptions.Proxy))
                    {
                        Environment.SetEnvironmentVariable("http_proxy", feedOptions.Proxy);
                    }

                    var success = await command.Execute();

                    return success ? 0 : 1;
                });
            });
        }

    }
}
