// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager
{
    internal static class RestoreConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory, IApplicationEnvironment applicationEnvironment)
        {
            cmdApp.Command("restore", c =>
            {
                c.Description = "Restore packages";

                var argRoot = c.Argument("[root]", "Root of all projects to restore. It can be a directory, a project.json, or a global.json.");
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
                    var feedOptions = feedCommandLineOptions.GetOptions();
                    var command = new RestoreCommand(applicationEnvironment);
                    command.Reports = reportsFactory.CreateReports(feedOptions.Quiet);
                    command.RestoreDirectory = argRoot.Value;
                    command.FeedOptions = feedOptions;
                    command.Lock = optLock.HasValue();
                    command.Unlock = optUnlock.HasValue();

                    if (!string.IsNullOrEmpty(feedOptions.Proxy))
                    {
                        Environment.SetEnvironmentVariable("http_proxy", feedOptions.Proxy);
                    }

                    var success = await command.ExecuteCommand();

                    return success ? 0 : 1;
                });
            });
        }

    }
}
