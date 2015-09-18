// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal static class ClearCacheConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory)
        {
            cmdApp.Command("clearcache", c =>
            {
                c.Description = "Clears the package cache.";
                c.OnExecute(() =>
                {
                    var command = new ClearCacheCommand(
                        reportsFactory.CreateReports(quiet: false),
                        DnuEnvironment.GetFolderPath(DnuFolderPath.HttpCacheDirectory));       
                    return command.Execute();
                });
            });
        }
    }
}
