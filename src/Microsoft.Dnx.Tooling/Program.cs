// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    public static class Program
    {
        public static int Main(string[] args)
        {
#if DNX451
            ServicePointManager.DefaultConnectionLimit = 1024;

            // Work around a Mono issue that makes restore unbearably slow,
            // due to some form of contention when requests are processed
            // concurrently. Restoring sequentially is *much* faster in this case.
            if (RuntimeEnvironmentHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 1;
            }
#endif

#if DEBUG
            // Add our own debug helper because DNU is usually run from a wrapper script,
            // making it too late to use the DNX one. Technically it's possible to use
            // the DNX_OPTIONS environment variable, but that's difficult to do per-command
            // on Windows
            if (args.Any(a => string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase)))
            {
                args = args.Where(a => !string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase)).ToArray();
                Console.WriteLine($"Process Id: {Process.GetCurrentProcess().Id}");
                Console.WriteLine("Waiting for Debugger to attach...");
                SpinWait.SpinUntil(() => Debugger.IsAttached);
            }
#endif

            var environment = PlatformServices.Default.Application;
            var runtimeEnv = PlatformServices.Default.Runtime;

            var app = new CommandLineApplication();
            app.Name = "dnu";
            app.FullName = "Microsoft .NET Development Utility";

            var optionVerbose = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", () => runtimeEnv.GetShortVersion(), () => runtimeEnv.GetFullVersion());

            // Show help information if no subcommand/option was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });

            // Defer reading option verbose until AFTER execute.
            var reportsFactory = new ReportsFactory(runtimeEnv, () => optionVerbose.HasValue());

            BuildConsoleCommand.Register(app, reportsFactory);
            CommandsConsoleCommand.Register(app, reportsFactory, environment);
            InstallConsoleCommand.Register(app, reportsFactory, environment);
            ListConsoleCommand.Register(app, reportsFactory, environment);
            PackConsoleCommand.Register(app, reportsFactory);
            PackagesConsoleCommand.Register(app, reportsFactory);
            PublishConsoleCommand.Register(app, reportsFactory, environment);
            RestoreConsoleCommand.Register(app, reportsFactory, environment, runtimeEnv);
            WrapConsoleCommand.Register(app, reportsFactory);
            FeedsConsoleCommand.Register(app, reportsFactory);
            ClearCacheConsoleCommand.Register(app, reportsFactory);

            try
            {
                return app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                AnsiConsole.GetError(useConsoleColor: runtimeEnv.OperatingSystem == "Windows").WriteLine($"Error: {ex.Message}".Red().Bold());
                ex.Command.ShowHelp();
                return 1;
            }
#if DEBUG
            catch
            {
                throw;
            }
#else
            catch (AggregateException aex)
            {
                foreach (var exception in aex.InnerExceptions)
                {
                    DumpException(exception, runtimeEnv);
                }
                return 1;
            }
            catch (Exception ex)
            {
                DumpException(exception, runtimeEnv);
                return 1;
            }
#endif
        }

        private static void DumpException(Exception ex, IRuntimeEnvironment runtimeEnv)
        {
            AnsiConsole
                .GetError(useConsoleColor: runtimeEnv.OperatingSystem == "Windows")
                .WriteLine($"Error: {ex.Message}".Red().Bold());
            Logger.TraceError($"Full Exception: {ex.ToString()}");
        }
    }
}
