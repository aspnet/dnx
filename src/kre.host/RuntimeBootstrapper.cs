// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace kre.host
{
    public static class RuntimeBootstrapper
    {
        private static readonly char[] _libPathSeparator = new[] { ';' };

        public static int Execute(string[] args)
        {
            // If we're a console host then print exceptions to stderr
            var printExceptionsToStdError = Environment.GetEnvironmentVariable(EnvironmentNames.ConsoleHost) == "1";

            try
            {
                return ExecuteAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (printExceptionsToStdError)
                {
                    PrintErrors(ex);
                    return 1;
                }

                throw;
            }
        }

        private static void PrintErrors(Exception ex)
        {
            while (ex != null)
            {
                if (ex is TargetInvocationException ||
                    ex is AggregateException)
                {
                    // Skip these exception messages as they are
                    // generic
                }
                else
                {
                    Console.Error.WriteLine(ex);
                }

                ex = ex.InnerException;
            }
        }

        public static Task<int> ExecuteAsync(string[] args)
        {
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = Constants.BootstrapperExeName;

            // RuntimeBootstrapper doesn't need to consume '--appbase' option because
            // klr/klr.cpp consumes the option value before invoking RuntimeBootstrapper
            // This is only for showing help info and swallowing useless '--appbase' option
            var optionAppbase = app.Option("--appbase <PATH>", "Application base directory path",
                CommandOptionType.SingleValue);
            var optionLib = app.Option("--lib <LIB_PATHS>", "Paths used for library look-up",
                CommandOptionType.MultipleValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion);

            // Options below are only for help info display
            // They will be forwarded to Microsoft.Framework.ApplicationHost
            var optionsToForward = new[]
            {
                app.Option("--watch", "Watch file changes", CommandOptionType.NoValue),
                app.Option("--packages <PACKAGE_DIR>", "Directory containing packages", CommandOptionType.SingleValue),
                app.Option("--configuration <CONFIGURATION>", "The configuration to run under", CommandOptionType.SingleValue),
                app.Option("--port <PORT>", "The port to the compilation server", CommandOptionType.SingleValue)
            };

            app.Execute(args);

            // Help information was already shown because help option was specified
            if (app.IsShowingInformation)
            {
                return Task.FromResult(0);
            }

            // Show help information if no subcommand/option was specified
            if (!app.IsShowingInformation && !app.RemainingArguments.Any())
            {
                app.ShowHelp();
                return Task.FromResult(2);
            }

            // Some options should be forwarded to Microsoft.Framework.ApplicationHost
            var appHostName = "Microsoft.Framework.ApplicationHost";
            var appHostIndex = app.RemainingArguments.FindIndex(s =>
                string.Equals(s, appHostName, StringComparison.OrdinalIgnoreCase));
            foreach (var option in optionsToForward)
            {
                if (option.HasValue())
                {
                    if (appHostIndex < 0)
                    {
                        Console.WriteLine("The option '--{0}' can only be used with {1}", option.LongName, appHostName);
                        return Task.FromResult(1);
                    }

                    if (option.OptionType == CommandOptionType.NoValue)
                    {
                        app.RemainingArguments.Insert(appHostIndex + 1, "--" + option.LongName);
                    }
                    else if (option.OptionType == CommandOptionType.SingleValue)
                    {
                        app.RemainingArguments.Insert(appHostIndex + 1, "--" + option.LongName);
                        app.RemainingArguments.Insert(appHostIndex + 2, option.Value());
                    }
                    else if (option.OptionType == CommandOptionType.MultipleValue)
                    {
                        foreach (var value in option.Values)
                        {
                            app.RemainingArguments.Insert(appHostIndex + 1, "--" + option.LongName);
                            app.RemainingArguments.Insert(appHostIndex + 2, value);
                        }
                    }
                }
            }

            // Resolve the lib paths
            string[] searchPaths = ResolveSearchPaths(optionLib.Values, app.RemainingArguments);

            var bootstrapper = new Bootstrapper(searchPaths);
            return bootstrapper.RunAsync(app.RemainingArguments.ToArray());
        }

        private static string[] ResolveSearchPaths(IEnumerable<string> libPaths, List<string> remainingArgs)
        {
            var searchPaths = new List<string>();

            var defaultLibPath = Environment.GetEnvironmentVariable(EnvironmentNames.DefaultLib);

            if (!string.IsNullOrEmpty(defaultLibPath))
            {
                // Add the default lib folder if specified
                searchPaths.AddRange(ExpandSearchPath(defaultLibPath));
            }

            // Add the expanded search libs to the list of paths
            searchPaths.AddRange(libPaths.SelectMany(ExpandSearchPath));

            // If a .dll or .exe is specified then turn this into
            // --lib {path to dll/exe} [dll/exe name]
            if (remainingArgs.Any())
            {
                var application = remainingArgs[0];
                var extension = Path.GetExtension(application);

                if (!string.IsNullOrEmpty(extension) &&
                    extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Add the directory to the list of search paths
                    searchPaths.Add(Path.GetDirectoryName(Path.GetFullPath(application)));

                    // Modify the argument to be the dll/exe name
                    remainingArgs[0] = Path.GetFileNameWithoutExtension(application);
                }
            }

            return searchPaths.ToArray();
        }

        private static IEnumerable<string> ExpandSearchPath(string libPath)
        {
            // Expand ; separated arguments
            return libPath.Split(_libPathSeparator, StringSplitOptions.RemoveEmptyEntries)
                          .Select(Path.GetFullPath);
        }

        private static string GetVersion()
        {
            var assembly = typeof(RuntimeBootstrapper).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (assemblyInformationalVersionAttribute == null)
            {
                return assembly.GetName().Version.ToString();
            }
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }
    }
}
