// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common;
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

            app.Command("build", c =>
            {
                c.Description = "Build the project in given directory";

                var optionFramework = c.Option("--framework <TARGET_FRAMEWORK>", "Target framework", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTPUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionCheckt = c.Option("--check", "Check diagnostics", CommandOptionType.NoValue);
                var optionDependencies = c.Option("--dependencies", "Copy dependencies", CommandOptionType.NoValue);
                var optionNative = c.Option("--native", "Generate native images", CommandOptionType.NoValue);
                var optionCrossgenPath = c.Option("--crossgenPath <PATH>", "Crossgen path", CommandOptionType.SingleValue);
                var optionRuntimePath = c.Option("--runtimePath <PATH>", "Runtime path", CommandOptionType.SingleValue);
                var argProjectDir = c.Argument("[project]", "Project to build, default is current directory");
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    var buildOptions = new BuildOptions();
                    buildOptions.RuntimeTargetFramework = _environment.TargetFramework;
                    buildOptions.OutputDir = optionOut.Value();
                    buildOptions.ProjectDir = argProjectDir.Value ?? Directory.GetCurrentDirectory();
                    buildOptions.CopyDependencies = optionDependencies.HasValue();
                    buildOptions.GenerateNativeImages = optionNative.HasValue();
                    buildOptions.RuntimePath = optionRuntimePath.Value();
                    buildOptions.CrossgenPath = optionCrossgenPath.Value();
                    buildOptions.CheckDiagnostics = optionCheckt.HasValue();

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
                    crossgenOptions.RuntimePath = optionOut.Value();
                    crossgenOptions.CrossgenPath = optionOut.Value();
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
