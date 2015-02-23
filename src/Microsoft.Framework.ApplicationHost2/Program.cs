// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;
using Microsoft.Framework.Runtime.Hosting;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.ApplicationHost
{
    public class Program
    {
        private readonly IAssemblyLoaderContainer _container;
        private readonly IApplicationEnvironment _environment;
        private readonly IServiceProvider _serviceProvider;

        public Program(IAssemblyLoaderContainer container, IApplicationEnvironment environment, IServiceProvider serviceProvider)
        {
            _container = container;
            _environment = environment;
            _serviceProvider = serviceProvider;
        }

        public Task<int> Main(string[] args)
        {
            Logger.TraceInformation("[ApplicationHost] Application Host Starting");
            ApplicationHostOptions options;
            string[] programArgs;
            int exitCode;

            bool shouldExit = ParseArgs(args, out options, out programArgs, out exitCode);
            if (shouldExit)
            {
                return Task.FromResult(exitCode);
            }


            // Construct the necessary context for hosting the application
            var builder = new RuntimeHostBuilder(options.ApplicationBaseDirectory);

            // Boot the runtime
            var host = builder.Build();

            // Check for a project
            if (host.Project == null)
            {
                // No project was found. We can't start the app.
                Logger.TraceError($"[ApplicationHost] A project.json file was not found in '{options.ApplicationBaseDirectory}'");
                return Task.FromResult(1);
            }

            // Get the project and print some information from it
            Logger.TraceInformation($"[ApplicationHost] Project: {host.Project.Metadata.Name} ({host.ApplicationBaseDirectory})");

            return Task.FromResult(0);
        }

        private static void DumpProperties(JObject properties, int indentLevel)
        {
            foreach (var prop in properties.Properties())
            {
                var indent = new string(' ', indentLevel);
                if (prop.Value.Type == JTokenType.Object)
                {
                    Console.WriteLine(indent + prop.Name + " = {");
                    DumpProperties((JObject)prop.Value, indentLevel + 1);
                    Console.WriteLine(indent + "}");
                }
                else
                {
                    Console.WriteLine(indent + $"{prop.Name} = [{prop.Value.GetType().Name}]\"{prop.Value}\"");
                }
            }
        }

        private bool ParseArgs(string[] args, out ApplicationHostOptions options, out string[] outArgs, out int exitCode)
        {
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = typeof(Program).Namespace;
            var optionWatch = app.Option("--watch", "Watch file changes", CommandOptionType.NoValue);
            var optionPackages = app.Option("--packages <PACKAGE_DIR>", "Directory containing packages",
                CommandOptionType.SingleValue);
            var optionConfiguration = app.Option("--configuration <CONFIGURATION>", "The configuration to run under", CommandOptionType.SingleValue);
            var optionCompilationServer = app.Option("--port <PORT>", "The port to the compilation server", CommandOptionType.SingleValue);
            var runCmdExecuted = false;
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion());
            var runCmd = app.Command("run", c =>
            {
                // We don't actually execute "run" command here
                // We are adding this command for the purpose of displaying correct help information
                c.Description = "Run application";
                c.OnExecute(() =>
                {
                    runCmdExecuted = true;
                    return 0;
                });
            },
            addHelpCommand: false,
            throwOnUnexpectedArg: false);
            app.Execute(args);

            options = null;
            outArgs = null;
            exitCode = 0;

            if (app.IsShowingInformation)
            {
                // If help option or version option was specified, exit immediately with 0 exit code
                return true;
            }
            else if (!(app.RemainingArguments.Any() || runCmdExecuted))
            {
                // If no subcommand was specified, show error message
                // and exit immediately with non-zero exit code
                Logger.TraceError("Please specify the command to run");
                exitCode = 2;
                return true;
            }

            options = new ApplicationHostOptions();
            options.WatchFiles = optionWatch.HasValue();
            options.PackageDirectory = optionPackages.Value();

            options.TargetFramework = _environment.RuntimeFramework;
            options.Configuration = optionConfiguration.Value() ?? _environment.Configuration ?? "Debug";
            options.ApplicationBaseDirectory = _environment.ApplicationBasePath;
            var portValue = optionCompilationServer.Value() ?? Environment.GetEnvironmentVariable(EnvironmentNames.CompilationServerPort);

            int port;
            if (!string.IsNullOrEmpty(portValue) && int.TryParse(portValue, out port))
            {
                options.CompilationServerPort = port;
            }

            var remainingArgs = new List<string>();
            if (runCmdExecuted)
            {
                // Later logic will execute "run" command
                // So we put this argment back after it was consumed by parser
                remainingArgs.Add("run");
                remainingArgs.AddRange(runCmd.RemainingArguments);
            }
            else
            {
                remainingArgs.AddRange(app.RemainingArguments);
            }

            if (remainingArgs.Any())
            {
                options.ApplicationName = remainingArgs[0];
                outArgs = remainingArgs.Skip(1).ToArray();
            }
            else
            {
                outArgs = remainingArgs.ToArray();
            }

            return false;
        }

        private static string GetVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }
    }
}