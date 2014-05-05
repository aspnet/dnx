using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Net.PackageManager.CommandLine;
using Microsoft.Net.PackageManager.Packing;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Common;
using System.Diagnostics;
using System.Threading;
using System.Net;

namespace Microsoft.Net.PackageManager
{
    public class Program : IReport
    {
        private readonly IApplicationEnvironment _environment;

        public Program(IApplicationEnvironment environment)
        {
            _environment = environment;
            Thread.GetDomain().SetData(".appDomain", this);
            ServicePointManager.DefaultConnectionLimit = 1024;
        }

        public int Main(string[] args)
        {
            var app = new CommandLineApplication();

            var optionVerbose = app.Option("-v|--verbose", "Show verbose output");
            var optionHelp = app.Option("-h|--help", "Show command help");
            var optionHelp2 = app.Option("-?", "Show command help");
            Func<bool> showHelp = () => optionHelp.Value != null || optionHelp2.Value != null;

            app.Command("restore", c =>
            {
                c.Description = "Restore packages";

                var argProject = c.Argument("[project]", "Project to restore, default is current directory");

                c.OnExecute(() =>
                {
                    if (showHelp()) { return app.Execute("help", "restore"); }

                    try
                    {
                        var command = new RestoreCommand(_environment);
                        command.Report = this;
                        command.RestoreDirectory = argProject.Value;
                        command.ExecuteCommand();

                        return 0;
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

            app.Command("install", c =>
            {
                c.Description = "Install package to project";

                var argProject = c.Argument("[package]", "Package Id to install");
                var optionOut = c.Option("--version <SEMVER>", "Version of the package to install, default is latest");
                var optionPrerelease = c.Option("--prerelease", "Use prerelease packages from the feed");

                c.OnExecute(() =>
                {
                    if (showHelp()) { return app.Execute("help", "install"); }

                    return 0;
                });
            });

            app.Command("pack", c =>
            {
                c.Description = "Bundle application for deployment";

                var argProject = c.Argument("[project]", "Path to project, default is current directory");
                var optionOut = c.Option("-o|--out <PATH>", "Where does it go");
                var optionZipPackages = c.Option("-z|--zippackages", "Bundle a zip full of packages");
                var optionOverwrite = c.Option("--overwrite", "Remove existing files in target folders");

                c.OnExecute(() =>
                {
                    if (showHelp()) { return app.Execute("help", "pack"); }

                    Console.WriteLine("verbose:{0} out:{1} zip:{2} project:{3}",
                        optionVerbose.Value,
                        optionOut.Value,
                        optionZipPackages.Value,
                        argProject.Value);

                    var options = new PackOptions
                    {
                        OutputDir = optionOut.Value,
                        ProjectDir = argProject.Value ?? System.IO.Directory.GetCurrentDirectory(),
                        RuntimeTargetFramework = _environment.TargetFramework,
                        ZipPackages = optionZipPackages.Value != null,
                        Overwrite = optionOverwrite.Value != null,
                    };

                    var manager = new PackManager(options);
                    if (!manager.Package())
                    {
                        return -1;
                    }

                    return 0;
                });
            });

            app.Command("help", c =>
            {
                c.Description = "Display help";

                var argCommand = c.Argument("command", "Display help for a specific command");

                c.OnExecute(() =>
                {
                    DisplayHelp(app, argCommand.Value);
                    return 0;
                });
            });

            app.OnExecute(() => app.Execute("help"));

            return app.Execute(args);
        }

        void DisplayHelp(CommandLineApplication app, string commandName)
        {
            if (commandName == null)
            {
                Console.WriteLine("kpm [command] [options] ...");
                Console.WriteLine();
                Console.WriteLine("Commands:");
                foreach (var command in app.Commands)
                {
                    Console.WriteLine("  {0}: {1}", command.Name, command.Description);
                }
            }
            else
            {
                var command = app.Commands.SingleOrDefault(cmd => String.Equals(cmd.Name, commandName, StringComparison.OrdinalIgnoreCase));
                if (command == null)
                {
                    Console.WriteLine("Unknown command {0}", commandName);
                }
                else
                {
                    var line = command.Name;
                    if (command.Options.Count != 0)
                    {
                        line += " [options]";
                    }
                    foreach (var argument in command.Arguments)
                    {
                        line += " " + argument.Name;
                    }
                    Console.WriteLine(command.Description);
                    Console.WriteLine();
                    Console.WriteLine("usage: {0}", line);

                    if (command.Arguments.Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine("Arguments:");
                        foreach (var argument in command.Arguments)
                        {
                            Console.WriteLine("  {0}  {1}", argument.Name, argument.Description);
                        }
                    }
                    if (command.GetAllOptions().Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine("Options:");
                        foreach (var option in command.GetAllOptions())
                        {
                            Console.WriteLine("  {0}  {1}", option.Template, option.Description);
                        }
                    }
                }
            }
        }

        public void WriteLine(string message)
        {
            Trace.WriteLine(message);
            Console.WriteLine(message);
        }
    }
}
