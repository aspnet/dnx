// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.CommandParsing;
using Microsoft.Dnx.Runtime.Common;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.ApplicationHost
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            DefaultHostOptions options;
            string[] programArgs;
            int exitCode;
            DefaultHost host;

            try
            {
                bool shouldExit = ParseArgs(args, out options, out programArgs, out exitCode);
                if (shouldExit)
                {
                    return exitCode;
                }

                host = new DefaultHost(options, PlatformServices.Default.AssemblyLoadContextAccessor);

                if (host.Project == null)
                {
                    return 1;
                }

                var lookupCommand = string.IsNullOrEmpty(options.ApplicationName) ? "run" : options.ApplicationName;
                string replacementCommand;
                if (host.Project.Commands.TryGetValue(lookupCommand, out replacementCommand))
                {
                    // preserveSurroundingQuotes: false to imitate a shell. Shells remove quotation marks before calling
                    // Main methods. Here however we are invoking Main() without involving a shell.
                    var replacementArgs = CommandGrammar
                        .Process(replacementCommand, GetVariable, preserveSurroundingQuotes: false)
                        .ToArray();
                    options.ApplicationName = replacementArgs.First();
                    programArgs = replacementArgs.Skip(1).Concat(programArgs).ToArray();
                }

                if (string.IsNullOrEmpty(options.ApplicationName) ||
                    string.Equals(options.ApplicationName, "run", StringComparison.Ordinal))
                {
                    options.ApplicationName = host.Project.EntryPoint ?? host.Project.Name;
                }
            }
            catch (Exception ex)
            {
                throw SuppressStackTrace(ex);
            }

            IDisposable disposable = null;

            try
            {
                disposable = host.AddLoaders(PlatformServices.Default.AssemblyLoaderContainer);

                return ExecuteMain(host, options.ApplicationName, programArgs)
                        .ContinueWith((t, state) =>
                        {
                            ((IDisposable)state).Dispose();
                            return t.GetAwaiter().GetResult();
                        },
                        disposable).GetAwaiter().GetResult();
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

        private static string GetVariable(string key)
        {
            var environment = PlatformServices.Default.Application;
            if (string.Equals(key, "env:ApplicationBasePath", StringComparison.OrdinalIgnoreCase))
            {
                return environment.ApplicationBasePath;
            }
            if (string.Equals(key, "env:ApplicationName", StringComparison.OrdinalIgnoreCase))
            {
                return environment.ApplicationName;
            }
            if (string.Equals(key, "env:Version", StringComparison.OrdinalIgnoreCase))
            {
                return environment.ApplicationVersion;
            }
            if (string.Equals(key, "env:TargetFramework", StringComparison.OrdinalIgnoreCase))
            {
                return environment.RuntimeFramework.Identifier;
            }
            return Environment.GetEnvironmentVariable(key);
        }

        private  static bool ParseArgs(string[] args, out DefaultHostOptions defaultHostOptions, out string[] outArgs, out int exitCode)
        {
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "Microsoft.Dnx.ApplicationHost";
            app.FullName = app.Name;

            // TODO: Remove this when the final beta8 tooling is released
            var optionWatch = app.Option("--watch", "Watch is deprecated, use Microsoft.Dnx.Watcher instead.", CommandOptionType.NoValue);
            var optionConfiguration = app.Option("--configuration <CONFIGURATION>", "The configuration to run under", CommandOptionType.SingleValue);
            var optionCompilationServer = app.Option("--port <PORT>", "The port to the compilation server", CommandOptionType.SingleValue);

            var runCmdExecuted = false;
            app.HelpOption("-?|-h|--help");

            var env = PlatformServices.Default.Runtime;
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
            var environment = PlatformServices.Default.Application;
            defaultHostOptions = new DefaultHostOptions();

            defaultHostOptions.TargetFramework = environment.RuntimeFramework;
            defaultHostOptions.Configuration = optionConfiguration.Value() ?? "Debug";
            defaultHostOptions.ApplicationBaseDirectory = environment.ApplicationBasePath;
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

        private static Task<int> ExecuteMain(DefaultHost host, string applicationName, string[] args)
        {
            Assembly assembly = null;

            try
            {
                assembly = host.GetEntryPoint(applicationName);
            }
            catch (Exception ex)
            {
                SuppressCompilationExceptions(ex);
                if (ex is FileLoadException || ex is FileNotFoundException)
                {
                    ThrowEntryPointNotfoundException(
                        host,
                        applicationName,
                        ex.InnerException);
                }

                throw;
            }

            if (assembly == null)
            {
                return Task.FromResult(1);
            }

            return Task.Run(()=> EntryPointExecutor.Execute(assembly, args, host.ServiceProvider))
                        .ContinueWith(t =>
                        {
                            t.Exception?.Handle(e =>
                            {
                                SuppressCompilationExceptions(e);
                                return false;
                            });

                            return t.Result;
                        });

        }

        private static void SuppressCompilationExceptions(Exception exception)
        {
            // Try to find compilation exception recursively to supress its stack
            var innerException = exception;
            do
            {
                if (innerException is ICompilationException)
                {
                    throw SuppressStackTrace(innerException);
                }
                innerException = innerException.InnerException;
            }
            while (innerException != null);
        }

        private static void ThrowEntryPointNotfoundException(
            DefaultHost host,
            string applicationName,
            Exception innerException)
        {
            var commands = host.Project.Commands;
            throw SuppressStackTrace(new InvalidOperationException(
                $"Unable to load application or execute command '{applicationName}'.{(commands.Any() ? $" Available commands: {string.Join(", ", commands.Keys)}.": "")}",
                innerException));
        }

        private static T SuppressStackTrace<T>(T ex) where T : Exception
        {
            ex.Data["suppressStackTrace"] = true;
            return ex;
        }
    }
}
