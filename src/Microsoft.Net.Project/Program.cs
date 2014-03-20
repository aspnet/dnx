using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Common;
using Microsoft.Net.Runtime.Common.CommandLine;

namespace Microsoft.Net.Project
{
    public class Program
    {
        private static readonly Dictionary<string, CommandOptionType> _buildOptions = new Dictionary<string, CommandOptionType>
        {
            { "framework", CommandOptionType.MultipleValue },
            { "out", CommandOptionType.SingleValue },
            { "dependencies", CommandOptionType.NoValue }
        };

        private static readonly Dictionary<string, CommandOptionType> _crossgenOptions = new Dictionary<string, CommandOptionType>
        {
            { "in", CommandOptionType.MultipleValue },
            { "out", CommandOptionType.SingleValue },
            { "exePath", CommandOptionType.SingleValue },
            { "runtimePath", CommandOptionType.SingleValue }
        };

        private readonly IApplicationEnvironment _environment;

        public Program(IApplicationEnvironment environment)
        {
            _environment = environment;
        }

        public int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("[command] [options]");
                return -1;
            }

            string command = args[0];

            try
            {
                if (command.Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    var parser = new CommandLineParser();
                    CommandOptions options;
                    parser.ParseOptions(args.Skip(1).ToArray(), _buildOptions, out options);

                    var buildOptions = new BuildOptions();
                    buildOptions.RuntimeTargetFramework = _environment.TargetFramework;
                    buildOptions.OutputDir = options.GetValue("out");
                    buildOptions.ProjectDir = options.RemainingArgs.Count > 0 ? options.RemainingArgs[0] : Directory.GetCurrentDirectory();
                    buildOptions.CopyDependencies = options.HasOption("dependencies");

                    var projectManager = new BuildManager(buildOptions);

                    if (!projectManager.Build())
                    {
                        return -1;
                    }
                }
                else if (command.Equals("crossgen", StringComparison.OrdinalIgnoreCase))
                {
                    var parser = new CommandLineParser();
                    CommandOptions options;
                    parser.ParseOptions(args.Skip(1).ToArray(), _crossgenOptions, out options);

                    var crossgenOptions = new CrossgenOptions();
                    crossgenOptions.InputPaths = options.GetValues("in") ?? Enumerable.Empty<string>();
                    crossgenOptions.RuntimePath = options.GetValue("runtimePath");
                    crossgenOptions.CrossgenPath = options.GetValue("exePath");

                    var gen = new CrossgenManager(crossgenOptions);
                    gen.GenerateNativeImages();
                }
                else
                {
                    Console.WriteLine("unknown command '{0}'", command);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, ExceptionHelper.GetExceptions(ex)));
                return -2;
            }

            return 0;
        }
    }
}
