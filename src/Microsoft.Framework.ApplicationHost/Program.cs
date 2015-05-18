// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.ApplicationHost.Impl.Syntax;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common;
using Microsoft.Framework.Runtime.Common.CommandLine;

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
            DefaultHostOptions options;
            string[] programArgs;
            int exitCode;

            bool shouldExit = ParseArgs(args, out options, out programArgs, out exitCode);
            if (shouldExit)
            {
                return Task.FromResult(exitCode);
            }

            var host = new DefaultHost(options, _serviceProvider);

            if (host.Project == null)
            {
                return Task.FromResult(-1);
            }

            var lookupCommand = string.IsNullOrEmpty(options.ApplicationName) ? "run" : options.ApplicationName;
            string replacementCommand;
            if (host.Project.Commands.TryGetValue(lookupCommand, out replacementCommand))
            {
                var replacementArgs = CommandGrammar.Process(
                    replacementCommand,
                    GetVariable).ToArray();
                options.ApplicationName = replacementArgs.First();
                programArgs = replacementArgs.Skip(1).Concat(programArgs).ToArray();
            }

            if (string.IsNullOrEmpty(options.ApplicationName) ||
                string.Equals(options.ApplicationName, "run", StringComparison.Ordinal))
            {
                options.ApplicationName = host.Project.EntryPoint ?? host.Project.Name;
            }

            IDisposable disposable = null;

            try
            {
                disposable = host.AddLoaders(_container);

                return ExecuteMain(host, options.ApplicationName, programArgs)
                        .ContinueWith(async (t, state) =>
                        {
                            ((IDisposable)state).Dispose();
                            return await t;
                        },
                        disposable).Unwrap();
            }
            catch
            {
                // If there's an error, dispose the host and throw
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                throw;
            }
        }

        private string GetVariable(string key)
        {
            if (string.Equals(key, "env:ApplicationBasePath", StringComparison.OrdinalIgnoreCase))
            {
                return _environment.ApplicationBasePath;
            }
            if (string.Equals(key, "env:ApplicationName", StringComparison.OrdinalIgnoreCase))
            {
                return _environment.ApplicationName;
            }
            if (string.Equals(key, "env:Version", StringComparison.OrdinalIgnoreCase))
            {
                return _environment.Version;
            }
            if (string.Equals(key, "env:TargetFramework", StringComparison.OrdinalIgnoreCase))
            {
                return _environment.RuntimeFramework.Identifier;
            }
            return Environment.GetEnvironmentVariable(key);
        }


        private bool ParseArgs(string[] args, out DefaultHostOptions defaultHostOptions, out string[] outArgs, out int exitCode)
        {
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "Microsoft.Framework.ApplicationHost";
            app.FullName = app.Name;
            var optionWatch = app.Option("--watch", "Watch file changes", CommandOptionType.NoValue);
            var optionPackages = app.Option("--packages <PACKAGE_DIR>", "Directory containing packages",
                CommandOptionType.SingleValue);
            var optionConfiguration = app.Option("--configuration <CONFIGURATION>", "The configuration to run under", CommandOptionType.SingleValue);
            var optionCompilationServer = app.Option("--port <PORT>", "The port to the compilation server", CommandOptionType.SingleValue);
            var runCmdExecuted = false;
            app.HelpOption("-?|-h|--help");

            var env = (IRuntimeEnvironment)_serviceProvider.GetService(typeof(IRuntimeEnvironment));
            app.VersionOption("--version", () => env.GetShortVersion(), () => env.GetFullVersion());
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

            defaultHostOptions = null;
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
                Console.WriteLine("Please specify the command to run");
                exitCode = 2;
                return true;
            }

            defaultHostOptions = new DefaultHostOptions();
            defaultHostOptions.WatchFiles = optionWatch.HasValue();
            defaultHostOptions.PackageDirectory = optionPackages.Value();

            defaultHostOptions.TargetFramework = _environment.RuntimeFramework;
            defaultHostOptions.Configuration = optionConfiguration.Value() ?? _environment.Configuration ?? "Debug";
            defaultHostOptions.ApplicationBaseDirectory = _environment.ApplicationBasePath;
            var portValue = optionCompilationServer.Value() ?? Environment.GetEnvironmentVariable(EnvironmentNames.CompilationServerPort);

            int port;
            if (!string.IsNullOrEmpty(portValue) && int.TryParse(portValue, out port))
            {
                defaultHostOptions.CompilationServerPort = port;
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
                defaultHostOptions.ApplicationName = remainingArgs[0];
                outArgs = remainingArgs.Skip(1).ToArray();
            }
            else
            {
                outArgs = remainingArgs.ToArray();
            }

            return false;
        }

        private Task<int> ExecuteMain(DefaultHost host, string applicationName, string[] args)
        {
            Assembly assembly = null;

            try
            {
                assembly = host.GetEntryPoint(applicationName);
            }
            catch (FileNotFoundException ex) when (new AssemblyName(ex.FileName).Name == applicationName)
            {
                if (ex.InnerException is ICompilationException)
                {
                    throw ex.InnerException;
                }

                ThrowEntryPointNotfoundException(
                        host,
                        applicationName,
                        ex.InnerException);
            }

            if (assembly == null)
            {
                return Task.FromResult(-1);
            }

            return EntryPointExecutor.Execute(assembly, args, host.ServiceProvider);
        }

        private static void ThrowEntryPointNotfoundException(
            DefaultHost host,
            string applicationName,
            Exception innerException)
        {
            if (host.Project.Commands.Any())
            {
                // Throw a nicer exception message if the command
                // can't be found
                throw new InvalidOperationException(
                    string.Format("Unable to load application or execute command '{0}'. Available commands: {1}.",
                    applicationName,
                    string.Join(", ", host.Project.Commands.Keys)), innerException);
            }

            throw new InvalidOperationException(
                    string.Format("Unable to load application or execute command '{0}'.",
                    applicationName), innerException);
        }
    }
}
