// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using DependencyAnalyzer.Commands;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace DependencyAnalyzer
{
    public class Program
    {
        private readonly IApplicationEnvironment _environment;

        public Program(IApplicationEnvironment environment)
        {
            _environment = environment;
        }

        public int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "dpa";

            var optionVerbose = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            var optionToolsPath = app.Option("--tools-path", "", CommandOptionType.SingleValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion());

            // Show help information if no subcommand/option was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });

            app.Command("tpa", c =>
            {
                c.Description = "Build minimal trusted platform assembly list";

                var assemblyFolder = c.Argument("[assemblies]", "Path to the folder contains the assemblies from which the TPA is built from.");
                var tpaSourceFile = c.Argument("[tpa.cpp]", "Path to the source file where the TPA list is generated in place.");

                c.HelpOption("-?|-h|-help");
                c.OnExecute(() =>
                {
                    var command = new BuildTpaCommand(_environment, assemblyFolder.Value, tpaSourceFile.Value);

                    return command.Execute();
                });
            });

            app.Command("runtime", c =>
            {
                c.Description = "Build the minimal required runtime assemblies";

                var assemblyFolder = c.Argument("[assemblies]", "Path to the folder contains the assemblies from which the TPA is built from.");
                var outputFile = c.Argument("[output]", "Path to the file where the TPA list is saved to. If omitted, output to console");

                c.HelpOption("-?|-h|-help");
                c.OnExecute(() =>
                {
                    var command = new BuildRuntimeCommand(_environment, assemblyFolder.Value, outputFile.Value);

                    return command.Execute();
                });
            });

            return app.Execute(args);
        }

        private static string GetVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }
    }
}