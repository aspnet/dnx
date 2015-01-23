// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.Framework.PackageManager.Packages;
using Microsoft.Framework.PackageManager.List;
using Microsoft.Framework.PackageManager.Packing;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager
{
    public class Program
    {
        private readonly IServiceProvider _hostServices;
        private readonly IApplicationEnvironment _environment;

        public Program(IServiceProvider hostServices, IApplicationEnvironment environment)
        {
            _hostServices = hostServices;
            _environment = environment;

#if ASPNET50
            Thread.GetDomain().SetData(".appDomain", this);
            ServicePointManager.DefaultConnectionLimit = 1024;
#endif
        }

        public int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "kpm";

            var optionVerbose = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion());

            // Show help information if no subcommand/option was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });

            app.Command("restore", c =>
            {
                c.Description = "Restore packages";

                var argRoot = c.Argument("[root]", "Root of all projects to restore. It can be a directory, a project.json, or a global.json.");
                var optSource = c.Option("-s|--source <FEED>", "A list of packages sources to use for this command",
                    CommandOptionType.MultipleValue);
                var optFallbackSource = c.Option("-f|--fallbacksource <FEED>",
                    "A list of packages sources to use as a fallback", CommandOptionType.MultipleValue);
                var optProxy = c.Option("-p|--proxy <ADDRESS>", "The HTTP proxy to use when retrieving packages",
                    CommandOptionType.SingleValue);
                var optNoCache = c.Option("--no-cache", "Do not use local cache", CommandOptionType.NoValue);
                var optPackageFolder = c.Option("--packages", "Path to restore packages", CommandOptionType.SingleValue);
                var optQuiet = c.Option("--quiet", "Do not show output such as HTTP request/cache information",
                    CommandOptionType.NoValue);
                var optIgnoreFailedSources = c.Option("--ignore-failed-sources",
                    "Ignore failed remote sources if there are local packages meeting version requirements",
                    CommandOptionType.NoValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () =>
                {
                    var command = new RestoreCommand(_environment);
                    command.Reports = CreateReports(optionVerbose.HasValue(), optQuiet.HasValue());

                    command.RestoreDirectory = argRoot.Value;
                    command.Sources = optSource.Values;
                    command.FallbackSources = optFallbackSource.Values;
                    command.NoCache = optNoCache.HasValue();
                    command.PackageFolder = optPackageFolder.Value();
                    command.IgnoreFailedSources = optIgnoreFailedSources.HasValue();

                    if (optProxy.HasValue())
                    {
                        Environment.SetEnvironmentVariable("http_proxy", optProxy.Value());
                    }

                    var success = await command.ExecuteCommand();

                    return success ? 0 : 1;
                });
            });

            app.Command("pack", c =>
            {
                c.Description = "Bundle application for deployment";

                var argProject = c.Argument("[project]", "Path to project, default is current directory");
                var optionOut = c.Option("-o|--out <PATH>", "Where does it go", CommandOptionType.SingleValue);
                var optionConfiguration = c.Option("--configuration <CONFIGURATION>", "The configuration to use for deployment (Debug|Release|{Custom})",
                    CommandOptionType.SingleValue);
                var optionOverwrite = c.Option("--overwrite", "Remove existing files in target folders",
                    CommandOptionType.NoValue);
                var optionNoSource = c.Option("--no-source", "Compiles the source files into NuGet packages",
                    CommandOptionType.NoValue);
                var optionRuntime = c.Option("--runtime <RUNTIME>", "Name or full path of the runtime folder to include",
                    CommandOptionType.MultipleValue);
                var optionNative = c.Option("--native", "Build and include native images. User must provide targeted CoreCLR runtime versions along with this option.",
                    CommandOptionType.NoValue);
                var optionWwwRoot = c.Option("--wwwroot <NAME>", "Name of public folder in the project directory",
                    CommandOptionType.SingleValue);
                var optionWwwRootOut = c.Option("--wwwroot-out <NAME>",
                    "Name of public folder in the packed image, can be used only when the '--wwwroot' option or 'webroot' in project.json is specified",
                    CommandOptionType.SingleValue);
                var optionQuiet = c.Option("--quiet", "Do not show output such as source/destination of packed files",
                    CommandOptionType.NoValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    var options = new PackOptions
                    {
                        OutputDir = optionOut.Value(),
                        ProjectDir = argProject.Value ?? System.IO.Directory.GetCurrentDirectory(),
                        Configuration = optionConfiguration.Value() ?? "Debug",
                        RuntimeTargetFramework = _environment.RuntimeFramework,
                        WwwRoot = optionWwwRoot.Value(),
                        WwwRootOut = optionWwwRootOut.Value() ?? optionWwwRoot.Value(),
                        Overwrite = optionOverwrite.HasValue(),
                        NoSource = optionNoSource.HasValue(),
                        Runtimes = optionRuntime.HasValue() ?
                            string.Join(";", optionRuntime.Values).
                                Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) :
                            new string[0],
                        Native = optionNative.HasValue(),
                        Reports = CreateReports(optionVerbose.HasValue(), optionQuiet.HasValue())
                    };

                    var manager = new PackManager(_hostServices, options);
                    if (!manager.Package())
                    {
                        return -1;
                    }

                    return 0;
                });
            });

            app.Command("build", c =>
            {
                c.Description = "Build NuGet packages for the project in given directory";

                var optionFramework = c.Option("--framework <TARGET_FRAMEWORK>", "A list of target frameworks to build.", CommandOptionType.MultipleValue);
                var optionConfiguration = c.Option("--configuration <CONFIGURATION>", "A list of configurations to build.", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTPUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionDependencies = c.Option("--dependencies", "Copy dependencies", CommandOptionType.NoValue);
                var optionQuiet = c.Option("--quiet", "Do not show output such as source/destination of nupkgs",
                    CommandOptionType.NoValue);
                var argProjectDir = c.Argument("[project]", "Project to build, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    var buildOptions = new BuildOptions();
                    buildOptions.OutputDir = optionOut.Value();
                    buildOptions.ProjectDir = argProjectDir.Value ?? Directory.GetCurrentDirectory();
                    buildOptions.Configurations = optionConfiguration.Values;
                    buildOptions.TargetFrameworks = optionFramework.Values;
                    buildOptions.Reports = CreateReports(optionVerbose.HasValue(), optionQuiet.HasValue());

                    var projectManager = new BuildManager(_hostServices, buildOptions);

                    if (!projectManager.Build())
                    {
                        return -1;
                    }

                    return 0;
                });
            });

            app.Command("add", c =>
            {
                c.Description = "Add a dependency into dependencies section of project.json";

                var argName = c.Argument("[name]", "Name of the dependency to add");
                var argVersion = c.Argument("[version]", "Version of the dependency to add");
                var argProject = c.Argument("[project]", "Path to project, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    var reports = CreateReports(optionVerbose.HasValue(), quiet: false);

                    var command = new AddCommand();
                    command.Reports = reports;
                    command.Name = argName.Value;
                    command.Version = argVersion.Value;
                    command.ProjectDir = argProject.Value;

                    var success = command.ExecuteCommand();

                    return success ? 0 : 1;
                });
            });

            app.Command("install", c =>
            {
                c.Description = "Install the given dependency";

                var argName = c.Argument("[name]", "Name of the dependency to add");
                var argVersion = c.Argument("[version]", "Version of the dependency to add, default is the latest version.");
                var argProject = c.Argument("[project]", "Path to project, default is current directory");
                var optSource = c.Option("-s|--source <FEED>", "A list of packages sources to use for this command",
                    CommandOptionType.MultipleValue);
                var optFallbackSource = c.Option("-f|--fallbacksource <FEED>",
                    "A list of packages sources to use as a fallback", CommandOptionType.MultipleValue);
                var optProxy = c.Option("-p|--proxy <ADDRESS>", "The HTTP proxy to use when retrieving packages",
                    CommandOptionType.SingleValue);
                var optNoCache = c.Option("--no-cache", "Do not use local cache", CommandOptionType.NoValue);
                var optPackageFolder = c.Option("--packages", "Path to restore packages", CommandOptionType.SingleValue);
                var optQuiet = c.Option("--quiet", "Do not show output such as HTTP request/cache information",
                    CommandOptionType.NoValue);
                var optIgnoreFailedSources = c.Option("--ignore-failed-sources",
                    "Ignore failed remote sources if there are local packages meeting version requirements",
                    CommandOptionType.NoValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () =>
                {
                    var reports = CreateReports(optionVerbose.HasValue(), optQuiet.HasValue());

                    var addCmd = new AddCommand();
                    addCmd.Reports = reports;
                    addCmd.Name = argName.Value;
                    addCmd.Version = argVersion.Value;
                    addCmd.ProjectDir = argProject.Value;

                    var restoreCmd = new RestoreCommand(_environment);
                    restoreCmd.Reports = reports;

                    restoreCmd.RestoreDirectory = argProject.Value;
                    restoreCmd.Sources = optSource.Values;
                    restoreCmd.FallbackSources = optFallbackSource.Values;
                    restoreCmd.NoCache = optNoCache.HasValue();
                    restoreCmd.PackageFolder = optPackageFolder.Value();
                    restoreCmd.IgnoreFailedSources = optIgnoreFailedSources.HasValue();

                    if (optProxy.HasValue())
                    {
                        Environment.SetEnvironmentVariable("http_proxy", optProxy.Value());
                    }

                    var installCmd = new InstallCommand(addCmd, restoreCmd);
                    installCmd.Reports = reports;

                    var success = await installCmd.ExecuteCommand();

                    return success ? 0 : 1;
                });
            });

            app.Command("packages", packagesCommand =>
            {
                packagesCommand.Description = "Commands related to managing local and remote packages folders";
                packagesCommand.HelpOption("-?|-h|--help");
                packagesCommand.OnExecute(() =>
                {
                    packagesCommand.ShowHelp();
                    return 2;
                });

                packagesCommand.Command("add", c =>
                {
                    c.Description = "Add a NuGet package to the specified packages folder";
                    var argNupkg = c.Argument("[nupkg]", "Path to a NuGet package");
                    var argSource = c.Argument("[source]", "Path to packages folder");
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(async () =>
                    {
                        var options = new AddOptions
                        {
                            Reports = CreateReports(optionVerbose.HasValue(), quiet: false),
                            SourcePackages = argSource.Value,
                            NuGetPackage = argNupkg.Value
                        };
                        var command = new Packages.AddCommand(options);
                        var success = await command.Execute();
                        return success ? 0 : 1;
                    });
                });

                packagesCommand.Command("push", c =>
                {
                    c.Description = "Incremental copy of files from local packages to remote location";
                    var argRemote = c.Argument("[remote]", "Path to remote packages folder");
                    var argSource = c.Argument("[source]",
                        "Path to source packages folder, default is current directory");
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(() =>
                    {
                        var reports = CreateReports(optionVerbose.HasValue(), quiet: false);

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

                packagesCommand.Command("pull", c =>
                {
                    c.Description = "Incremental copy of files from remote location to local packages";
                    var argRemote = c.Argument("[remote]", "Path to remote packages folder");
                    var argSource = c.Argument("[source]",
                        "Path to source packages folder, default is current directory");
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(() =>
                    {
                        var reports = CreateReports(optionVerbose.HasValue(), quiet: false);

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
            });

            app.Command("list", c =>
            {
                c.Description = "Print the dependencies of a given project.";
                var showAssemblies = c.Option("-a|--assemblies",
                    "Show the assembly files that are depended on by given project.",
                    CommandOptionType.NoValue);
                var framework = c.Option("--framework",
                    "Show dependencies for only the given framework.",
                    CommandOptionType.SingleValue);
                var runtimeFolder = c.Option("--runtime",
                    "The folder containing all available framework assemblies.",
                    CommandOptionType.SingleValue);
                var argProject = c.Argument("[project]", "The path to project. If omitted, the command will use the project in the current directory.");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    var options = new DependencyListOptions(CreateReports(verbose: true, quiet: false), argProject, framework)
                    {
                        ShowAssemblies = showAssemblies.HasValue(),
                        RuntimeFolder = runtimeFolder.Value(),
                    };

                    if (!options.Valid)
                    {
                        if (options.Project == null)
                        {
                            options.Reports.Error.WriteLine(string.Format("A project could not be found in {0}.", options.Path).Red());
                            return 1;
                        }
                        else
                        {
                            options.Reports.Error.WriteLine("Invalid options.".Red());
                            return 2;
                        }
                    }

                    var command = new DependencyListCommand(options);
                    return command.Execute();
                });
            });

            // "kpm wrap" invokes MSBuild, which is not available on *nix
            if (!PlatformHelper.IsMono)
            {
                app.Command("wrap", c =>
                {
                    c.Description = "Wrap a csproj into a project.json, which can be referenced by project.json files";

                    var argPath = c.Argument("[path]", "Path to csproj to be wrapped");
                    var optConfiguration = c.Option("--configuration <CONFIGURATION>",
                        "Configuration of wrapped project, default is 'debug'", CommandOptionType.SingleValue);
                    var optMsBuildPath = c.Option("--msbuild <PATH>",
                        @"Path to MSBuild, default is '%ProgramFiles%\MSBuild\14.0\Bin\MSBuild.exe'",
                        CommandOptionType.SingleValue);
                    var optInPlace = c.Option("-i|--in-place",
                        "Generate or update project.json files in project directories of csprojs",
                        CommandOptionType.NoValue);
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(() =>
                    {
                        var reports = CreateReports(optionVerbose.HasValue(), quiet: false);

                        var command = new WrapCommand();
                        command.Reports = reports;
                        command.CsProjectPath = argPath.Value;
                        command.Configuration = optConfiguration.Value();
                        command.MsBuildPath = optMsBuildPath.Value();
                        command.InPlace = optInPlace.HasValue();

                        var success = command.ExecuteCommand();

                        return success ? 0 : 1;
                    });
                });
            }

            return app.Execute(args);
        }

        private Reports CreateReports(bool verbose, bool quiet)
        {
            IReport output = new Report(AnsiConsole.Output);
            var reports = new Reports()
            {
                Information = output,
                Verbose = verbose ? output : new NullReport(),
                Error = new Report(AnsiConsole.Output),
            };

            // If "--verbose" and "--quiet" are specified together, "--verbose" wins
            reports.Quiet = quiet ? reports.Verbose : output;

            return reports;
        }

        private static string GetVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }
    }
}