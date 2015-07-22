// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Project
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
            app.Name = "Microsoft.Dnx.Project";
            app.FullName = app.Name;
            app.HelpOption("-?|-h|--help");

            // Show help information if no subcommand/option was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });

            app.Command("crossgen", c =>
            {
                c.Description = "Do CrossGen";

                var optionIn = c.Option("--in <INPUT_DIR>", "Input directory", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTOUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionExePath = c.Option("--exePath", "Exe path", CommandOptionType.SingleValue);
                var optionRuntimePath = c.Option("--runtimePath <PATH>", "Runtime path", CommandOptionType.SingleValue);
                var optionSymbols = c.Option("--symbols", "Use symbols", CommandOptionType.NoValue);
                var optionPartial = c.Option("--partial", "Allow partial NGEN", CommandOptionType.NoValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(() =>
                {
                    var crossgenOptions = new CrossgenOptions();
                    crossgenOptions.InputPaths = optionIn.Values;
                    crossgenOptions.RuntimePath = optionRuntimePath.Value();
                    crossgenOptions.CrossgenPath = optionExePath.Value();
                    crossgenOptions.Symbols = optionSymbols.HasValue();
                    crossgenOptions.Partial = optionPartial.HasValue();

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
