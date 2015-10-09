// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Tooling.List;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal static class ListConsoleCommand
    {
        public static void Register(CommandLineApplication cmdApp, ReportsFactory reportsFactory, IApplicationEnvironment appEnvironment)
        {
            cmdApp.Command("list", (c =>
            {
                c.Description = "Print the dependencies of a given project";
                var showAssemblies = c.Option("-a|--assemblies",
                    "Show the assembly files that are depended on by given project",
                    CommandOptionType.NoValue);
                var frameworks = c.Option("--framework <TARGET_FRAMEWORK>",
                    "Show dependencies for only the given frameworks",
                    CommandOptionType.MultipleValue);
                var runtimeFolder = c.Option("--runtime <PATH>",
                    "The folder containing all available framework assemblies",
                    CommandOptionType.SingleValue);
                var details = c.Option("--details",
                    "Show the details of how each dependency is introduced",
                    CommandOptionType.NoValue);
                var mismatch = c.Option("--mismatch",
                    "Show the mismatch dependencies.",
                    CommandOptionType.NoValue);
                var single = c.Option("--single",
                    "Show details of one dependency and shows all the information about it.",
                    CommandOptionType.SingleValue);
                var resultsFilter = c.Option("--filter <PATTERN>",
                    "Filter the libraries referenced by the project base on their names. The matching pattern supports * and ?",
                    CommandOptionType.SingleValue);
                var argProject = c.Argument("[project]", "Path to project, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute((() =>
                {
                    c.ShowRootCommandFullNameAndVersion();

                    var options = new List.DependencyListOptions(reportsFactory.CreateReports(verbose: true, quiet: false), argProject)
                    {
                        ShowAssemblies = showAssemblies.HasValue(),
                        RuntimeFolder = runtimeFolder.Value(),
                        Details = details.HasValue(),
                        ResultsFilter = resultsFilter.Value(),
                        Mismatch = mismatch.HasValue(),
                        Single = single.Value()
                    };
                    options.AddFrameworkMonikers(frameworks.Values);

                    if (!options.Valid)
                    {
                        if (options.Project == null)
                        {
                            options.Reports.Error.WriteLine(string.Format("Unable to locate {0}.".Red(), Runtime.Project.ProjectFileName));
                            return 1;
                        }
                        else
                        {
                            options.Reports.Error.WriteLine("Invalid options.".Red());
                            return 2;
                        }
                    }

                    var command = new DependencyListCommand(options, appEnvironment.RuntimeFramework);
                    return command.Execute();
                }));
            }));
        }
    }
}
