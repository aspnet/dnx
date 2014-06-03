// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
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
        private readonly IHostContainer _container;
        private readonly IApplicationEnvironment _environment;
        private readonly IServiceProvider _serviceProvider;

        public Program(IHostContainer container, IApplicationEnvironment environment, IServiceProvider serviceProvider)
        {
            _container = container;
            _environment = environment;
            _serviceProvider = serviceProvider;
        }

        public Task<int> Main(string[] args)
        {
            DefaultHostOptions options;
            string[] programArgs;

            var isShowingHelp = ParseArgs(args, out options, out programArgs);
            if (isShowingHelp)
            {
                return Task.FromResult(0);
            }

            var host = new DefaultHost(options, _serviceProvider);

            if (host.Project == null)
            {
                return Task.FromResult(-1);
            }

            var lookupCommand = options.ApplicationName;
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
                string.Equals(options.ApplicationName, "run", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(host.Project.Name))
                {
                    options.ApplicationName = Path.GetFileName(options.ApplicationBaseDirectory);
                }
                else
                {
                    options.ApplicationName = host.Project.Name;
                }
            }

            IDisposable disposable = null;

            try
            {
                disposable = _container.AddHost(host);

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
                return _environment.TargetFramework.Identifier;
            }
            return Environment.GetEnvironmentVariable(key);
        }


        private bool ParseArgs(string[] args, out DefaultHostOptions defaultHostOptions, out string[] outArgs)
        {
            var localDefaultHostOptions = new DefaultHostOptions();
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            string appName = null;
            app.Name = "k";
            app.HelpOption("-?|-h|--help");
            app.Command("run", c =>
            {
                c.Description = "Run given application";

                c.HelpOption("-?|-h|--help");
                var argumentAppName = c.Argument("[appName]", "Application to be run");
                var optionWatch = c.Option("--watch", "Watch file changes", CommandOptionType.NoValue);
                var optionPackages = c.Option("--packages <PACKAGE_DIR>", "Directory contatining packages",
                    CommandOptionType.SingleValue);
                c.OnExecute(() =>
                {
                    localDefaultHostOptions.ApplicationName = c.Name;
                    localDefaultHostOptions.WatchFiles = optionWatch.HasValue();
                    localDefaultHostOptions.PackageDirectory = optionPackages.Value();
                    appName = argumentAppName.Value;
                    return 0;
                });
            });
            app.Execute(args);

            // If the user types simply a "k" without any argument or option
            if (!app.IsShowingHelp &&
                string.IsNullOrEmpty(localDefaultHostOptions.ApplicationName) &&
                !app.RemainingArguments.Any())
            {
                app.ShowHelp(commandName: null);
            }

            localDefaultHostOptions.TargetFramework = _environment.TargetFramework;
            localDefaultHostOptions.ApplicationBaseDirectory = _environment.ApplicationBasePath;

            if (!string.IsNullOrEmpty(appName))
            {
                app.RemainingArguments.Insert(0, appName);
                outArgs = app.RemainingArguments.ToArray();
            }
            else if (app.RemainingArguments.Any())
            {
                localDefaultHostOptions.ApplicationName = app.RemainingArguments[0];
                outArgs = app.RemainingArguments.Skip(1).ToArray();
            }
            else
            {
                outArgs = app.RemainingArguments.ToArray();
            }

            defaultHostOptions = localDefaultHostOptions;

            return app.IsShowingHelp;
        }

        private Task<int> ExecuteMain(DefaultHost host, string applicationName, string[] args)
        {
            Assembly assembly = null;

            try
            {
                assembly = host.GetEntryPoint(applicationName);
            }
            catch (FileLoadException ex)
            {
                // FileName is always turned into an assembly name
                if (new AssemblyName(ex.FileName).Name == applicationName)
                {
                    ThrowEntryPointNotfoundException(
                        host,
                        applicationName,
                        ex.InnerException);
                }
                else
                {
                    throw;
                }
            }
            catch (FileNotFoundException ex)
            {
                if (ex.FileName == applicationName)
                {
                    ThrowEntryPointNotfoundException(
                        host,
                        applicationName,
                        ex.InnerException);
                }
                else
                {
                    throw;
                }
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

            var compilationException = innerException as CompilationException;

            if (compilationException != null)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, compilationException.Errors));
            }

#if K10
            // HACK: Don't show inner exceptions for non compilation errors.
            // There's a bug in the CoreCLR loader, where it throws another
            // invalid operation exception for any load failure with a bizzare
            // message.
            innerException = null;
#endif

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
