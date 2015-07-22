// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal class InstallConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory, IApplicationEnvironment appEnvironment)
        {
            cmdApp.Command("install", c =>
            {
                c.Description = "Install the given dependency";

                var argName = c.Argument("[name]", "Name of the dependency to add");
                var argVersion = c.Argument("[version]", "Version of the dependency to add, default is the latest version.");
                var argProject = c.Argument("[project]", "Path to project, default is current directory");

                var feedCommandLineOptions = FeedCommandLineOptions.Add(c);

                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var feedOptions = feedCommandLineOptions.GetOptions();
                    var reports = reportsFactory.CreateReports(feedOptions.Quiet);

                    var addCmd = new AddCommand();
                    addCmd.Reports = reports;
                    addCmd.Name = argName.Value;
                    addCmd.Version = argVersion.Value;
                    addCmd.ProjectDir = argProject.Value;

                    var restoreCmd = new RestoreCommand(appEnvironment);
                    restoreCmd.Reports = reports;
                    restoreCmd.FeedOptions = feedOptions;

                    restoreCmd.RestoreDirectories.Add(argProject.Value);

                    if (!string.IsNullOrEmpty(feedOptions.Proxy))
                    {
                        Environment.SetEnvironmentVariable("http_proxy", feedOptions.Proxy);
                    }

                    var installCmd = new InstallCommand(addCmd, restoreCmd);
                    installCmd.Reports = reports;

                    var success = await installCmd.ExecuteCommand();

                    return success ? 0 : 1;
                });
            });

        }
    }
}
