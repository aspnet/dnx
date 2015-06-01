// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager
{
    public class Program
    {
        private readonly IServiceProvider _hostServices;
        private readonly IApplicationEnvironment _environment;
        private readonly IRuntimeEnvironment _runtimeEnv;

        public Program(IServiceProvider hostServices, IApplicationEnvironment environment, IRuntimeEnvironment runtimeEnv)
        {
            _hostServices = hostServices;
            _environment = environment;
            _runtimeEnv = runtimeEnv;

#if DNX451
            Thread.GetDomain().SetData(".appDomain", this);
            ServicePointManager.DefaultConnectionLimit = 1024;

            // Work around a Mono issue that makes restore unbearably slow,
            // due to some form of contention when requests are processed
            // concurrently. Restoring sequentially is *much* faster in this case.
            if (PlatformHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 1;
            }
#endif
        }

        public int Main(string[] args)
        {
            WaitForOrLaunchDebuggerIfRequested(ref args);

            var app = new CommandLineApplication();
            app.Name = "dnu";
            app.FullName = "Microsoft .NET Development Utility";

            var optionVerbose = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", () => _runtimeEnv.GetShortVersion(), () => _runtimeEnv.GetFullVersion());

            // Show help information if no subcommand/option was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });

            var reportsFactory = new ReportsFactory(_runtimeEnv, optionVerbose.HasValue());

            BuildConsoleCommand.Register(app, reportsFactory, _hostServices);
            CommandsConsoleCommand.Register(app, reportsFactory, _environment);
            InstallConsoleCommand.Register(app, reportsFactory, _environment);
            ListConsoleCommand.Register(app, reportsFactory, _environment);
            PackConsoleCommand.Register(app, reportsFactory, _hostServices);
            PackagesConsoleCommand.Register(app, reportsFactory);
            PublishConsoleCommand.Register(app, reportsFactory, _environment, _hostServices);
            RestoreConsoleCommand.Register(app, reportsFactory, _environment);
            WrapConsoleCommand.Register(app, reportsFactory);

            return app.Execute(args);
        }

        [Conditional("DEBUG")]
        // TODO(anurse): This seems like a broadly useful helper
        private static void WaitForOrLaunchDebuggerIfRequested(ref string[] args)
        {
            if (args.Length > 0 && args[0].Equals("--debug", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Waiting for Debugger to attach.");
#if DNX451
                Console.WriteLine("Attach a debugger or press ENTER to continue. Or, press L to trigger the just-in-time debugger.");
#endif
                Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

                while (true)
                {
                    if (Debugger.IsAttached)
                    {
                        break;
                    }
#if DNX451
                    else if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey().Key;
                        if (key == ConsoleKey.Enter)
                        {
                            break;
                        }
                        else if (key == ConsoleKey.L)
                        {
                            Debugger.Launch();
                            break;
                        }
                    }
#endif
                }
                var newargs = new string[args.Length - 1];
                Array.Copy(args, 1, newargs, 0, newargs.Length);
                args = newargs;
            }
        }
    }
}
