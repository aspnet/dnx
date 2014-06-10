// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Framework.PackageManager;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.Project
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
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "Microsoft.Framework.Project";
            app.HelpOption("-?|-h|--help");

            // Show help information if no subcommand was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            // This is temporary until we update the rest of the stack (including tooling)
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

            app.Command("crossgen", c =>
            {
                c.Description = "Do CrossGen";

                var optionIn = c.Option("--in <INPUT_DIR>", "Input directory", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTOUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionExePath = c.Option("--exePath", "Exe path", CommandOptionType.SingleValue);
                var optionRuntimePath = c.Option("--runtimePath <PATH>", "Runtime path", CommandOptionType.SingleValue);
                var optionSymbols = c.Option("--symbols", "Use symbols", CommandOptionType.NoValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    var crossgenOptions = new CrossgenOptions();
                    crossgenOptions.InputPaths = optionIn.Values;
                    crossgenOptions.RuntimePath = optionRuntimePath.Value();
                    crossgenOptions.CrossgenPath = optionExePath.Value();
                    crossgenOptions.Symbols = optionSymbols.HasValue();

                    var gen = new CrossgenManager(crossgenOptions);
                    if (!gen.GenerateNativeImages())
                    {
                        return -1;
                    }

                    return 0;
                });
            });

            app.Execute(args);

            return 0;
        }
    }
}
