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
        private static readonly Dictionary<string, CommandOptionType> _options = new Dictionary<string, CommandOptionType>
        {
            { "framework", CommandOptionType.MultipleValue },
            { "out", CommandOptionType.SingleValue },
            { "dependencies", CommandOptionType.NoValue }
        };

        public int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("[command] [options]");
                return -1;
            }

            string command = args[0];

            var parser = new CommandLineParser();
            CommandOptions options;
            parser.ParseOptions(args.Skip(1).ToArray(), _options, out options);

            var buildOptions = new BuildOptions();
            buildOptions.RuntimeTargetFramework = Environment.GetEnvironmentVariable("TARGET_FRAMEWORK") ?? "net45";
            buildOptions.OutputDir = options.GetValue("out");
            buildOptions.ProjectDir = options.RemainingArgs.Count > 0 ? options.RemainingArgs[0] : Directory.GetCurrentDirectory();
            buildOptions.CopyDependencies = options.HasOption("dependencies");

            var projectManager = new ProjectManager(buildOptions);

            try
            {
                if (command.Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    if (!projectManager.Build())
                    {
                        return -1;
                    }
                }
                else
                {
                    System.Console.WriteLine("unknown command '{0}'", command);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine(String.Join(Environment.NewLine, ExceptionHelper.GetExceptions(ex)));
                return -2;
            }

            return 0;
        }
    }
}
