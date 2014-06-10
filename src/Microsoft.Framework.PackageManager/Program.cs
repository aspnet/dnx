// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.Framework.PackageManager.Packing;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager
{
    public class Program : IReport
    {
        private readonly IApplicationEnvironment _environment;

        public Program(IApplicationEnvironment environment)
        {
            _environment = environment;

#if NET45
            Thread.GetDomain().SetData(".appDomain", this);
            ServicePointManager.DefaultConnectionLimit = 1024;
#endif
        }

        public int Main(string[] args)
        {
#if NET45
            _originalForeground = Console.ForegroundColor;
#endif

            var app = new CommandLineApplication();
            app.Name = "kpm";

            var optionVerbose = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            var optionVersion = app.Option("--version", "Show version information", CommandOptionType.NoValue);
            app.HelpOption("-?|-h|--help");

            // Show help information if no subcommand was specified
            app.OnExecute(() =>
            {
                if (optionVersion.HasValue())
                {
                    ShowVersion();
                    return 0;
                }

                app.ShowHelp();
                return 0;
            });

            app.Command("restore", c =>
            {
                c.Description = "Restore packages";

                var argProject = c.Argument("[project]", "Project to restore, default is current directory");
                var optSource = c.Option("-s|--source <FEED>", "A list of packages sources to use for this command",
                    CommandOptionType.MultipleValue);
                var optFallbackSource = c.Option("-f|--fallbacksource <FEED>",
                    "A list of packages sources to use as a fallback", CommandOptionType.MultipleValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    if (optionVersion.HasValue())
                    {
                        ShowVersion();
                        return 0;
                    }

                    try
                    {
                        var command = new RestoreCommand(_environment);
                        command.Report = this;
                        command.RestoreDirectory = argProject.Value;
                        if (optSource.HasValue())
                        {
                            command.Sources = optSource.Values;
                        }
                        if (optFallbackSource.HasValue())
                        {
                            command.FallbackSources = optFallbackSource.Values;
                        }
                        var success = command.ExecuteCommand();

                        return success ? 0 : 1;
                    }
                    catch (Exception ex)
                    {
                        this.WriteLine("----------");
                        this.WriteLine(ex.ToString());
                        this.WriteLine("----------");
                        this.WriteLine("Restore failed");
                        this.WriteLine(ex.Message);
                        return 1;
                    }
                });
            });

            app.Command("pack", c =>
            {
                c.Description = "Bundle application for deployment";

                var argProject = c.Argument("[project]", "Path to project, default is current directory");
                var optionOut = c.Option("-o|--out <PATH>", "Where does it go", CommandOptionType.SingleValue);
                var optionZipPackages = c.Option("-z|--zippackages", "Bundle a zip full of packages",
                    CommandOptionType.NoValue);
                var optionOverwrite = c.Option("--overwrite", "Remove existing files in target folders",
                    CommandOptionType.NoValue);
                var optionRuntime = c.Option("--runtime <KRE>", "Names or paths to KRE files to include",
                    CommandOptionType.MultipleValue);
                var optionAppFolder = c.Option("--appfolder <NAME>",
                    "Determine the name of the application primary folder", CommandOptionType.SingleValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    if (optionVersion.HasValue())
                    {
                        ShowVersion();
                        return 0;
                    }

                    Console.WriteLine("verbose:{0} out:{1} zip:{2} project:{3}",
                        optionVerbose.HasValue(),
                        optionOut.Value(),
                        optionZipPackages.HasValue(),
                        argProject.Value);

                    var options = new PackOptions
                    {
                        OutputDir = optionOut.Value(),
                        ProjectDir = argProject.Value ?? System.IO.Directory.GetCurrentDirectory(),
                        AppFolder = optionAppFolder.Value(),
                        RuntimeTargetFramework = _environment.TargetFramework,
                        ZipPackages = optionZipPackages.HasValue(),
                        Overwrite = optionOverwrite.HasValue(),
                        Runtimes = optionRuntime.HasValue() ?
                            string.Join(";", optionRuntime.Values).
                                Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) :
                            new string[0],
                    };

                    var manager = new PackManager(options);
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

                var optionFramework = c.Option("--framework <TARGET_FRAMEWORK>", "Target framework", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTPUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionCheck = c.Option("--check", "Check diagnostics", CommandOptionType.NoValue);
                var optionDependencies = c.Option("--dependencies", "Copy dependencies", CommandOptionType.NoValue);
                var argProjectDir = c.Argument("[project]", "Project to build, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    if (optionVersion.HasValue())
                    {
                        ShowVersion();
                        return 0;
                    }

                    var buildOptions = new BuildOptions();
                    buildOptions.RuntimeTargetFramework = _environment.TargetFramework;
                    buildOptions.OutputDir = optionOut.Value();
                    buildOptions.ProjectDir = argProjectDir.Value ?? Directory.GetCurrentDirectory();
                    buildOptions.CheckDiagnostics = optionCheck.HasValue();

                    var projectManager = new BuildManager(buildOptions);

                    if (!projectManager.Build())
                    {
                        return -1;
                    }

                    return 0;
                });
            });

            return app.Execute(args);
        }

        object _lock = new object();
        ConsoleColor _originalForeground;
        void SetColor(ConsoleColor color)
        {
#if NET45
            Console.ForegroundColor = (ConsoleColor)(((int)Console.ForegroundColor & 0x08) | ((int)color & 0x07));
#endif
        }

        void SetBold(bool bold)
        {
#if NET45
            Console.ForegroundColor = (ConsoleColor)(((int)Console.ForegroundColor & 0x07) | (bold ? 0x08 : 0x00));
#endif
        }

        public void WriteLine(string message)
        {
            var sb = new System.Text.StringBuilder();
            lock (_lock)
            {
                var escapeScan = 0;
                for (; ;)
                {
                    var escapeIndex = message.IndexOf("\x1b[", escapeScan);
                    if (escapeIndex == -1)
                    {
                        var text = message.Substring(escapeScan);
                        sb.Append(text);
                        Console.Write(text);
                        break;
                    }
                    else
                    {
                        var startIndex = escapeIndex + 2;
                        var endIndex = startIndex;
                        while (endIndex != message.Length &&
                            message[endIndex] >= 0x20 &&
                            message[endIndex] <= 0x3f)
                        {
                            endIndex += 1;
                        }

                        var text = message.Substring(escapeScan, escapeIndex - escapeScan);
                        sb.Append(text);
                        Console.Write(text);
                        if (endIndex == message.Length)
                        {
                            break;
                        }

                        switch (message[endIndex])
                        {
                            case 'm':
                                int value;
                                if (int.TryParse(message.Substring(startIndex, endIndex - startIndex), out value))
                                {
                                    switch (value)
                                    {
                                        case 1:
                                            SetBold(true);
                                            break;
                                        case 22:
                                            SetBold(false);
                                            break;
                                        case 30:
                                            SetColor(ConsoleColor.Black);
                                            break;
                                        case 31:
                                            SetColor(ConsoleColor.Red);
                                            break;
                                        case 32:
                                            SetColor(ConsoleColor.Green);
                                            break;
                                        case 33:
                                            SetColor(ConsoleColor.Yellow);
                                            break;
                                        case 34:
                                            SetColor(ConsoleColor.Blue);
                                            break;
                                        case 35:
                                            SetColor(ConsoleColor.Magenta);
                                            break;
                                        case 36:
                                            SetColor(ConsoleColor.Cyan);
                                            break;
                                        case 37:
                                            SetColor(ConsoleColor.Gray);
                                            break;
                                        case 39:
                                            SetColor(_originalForeground);
                                            break;
                                    }
                                }
                                break;
                        }

                        escapeScan = endIndex + 1;
                    }
                }
                Console.WriteLine();
            }
#if NET45
            Trace.WriteLine(sb.ToString());
#endif
        }

        private void ShowVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            Console.WriteLine(assemblyInformationalVersionAttribute.InformationalVersion);
        }
    }
}
