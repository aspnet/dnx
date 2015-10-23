// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
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
#if DNXCORE50
            var runtimeEnv = new RuntimeEnvironment2();
            PlatformServices.SetDefault(PlatformServices.Create(basePlatformServices: null, runtime: runtimeEnv));
#else
            var runtimeEnv = PlatformServices.Default.Runtime;
#endif

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

            // BuildConsoleCommand.Register(app, reportsFactory);
            // CommandsConsoleCommand.Register(app, reportsFactory, environment);
            // InstallConsoleCommand.Register(app, reportsFactory, environment);
            // ListConsoleCommand.Register(app, reportsFactory, environment);
            // PackConsoleCommand.Register(app, reportsFactory);
            // PackagesConsoleCommand.Register(app, reportsFactory);
            // PublishConsoleCommand.Register(app, reportsFactory, environment);
            RestoreConsoleCommand.Register(app, reportsFactory, runtimeEnv);
            // WrapConsoleCommand.Register(app, reportsFactory);
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
                DumpException(ex, runtimeEnv);
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

#if DNXCORE50
        private class RuntimeEnvironment2 : IRuntimeEnvironment
        {
            private string _osVersion;

            private string _osName;

            public RuntimeEnvironment2()
            {
                RuntimeType = "CoreCLR";
                RuntimeArchitecture = IntPtr.Size == 8 ? "x64" : "x86";

                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    _osName = RuntimeOperatingSystems.Windows;
                }
            }

            public string OperatingSystem
            {
                get
                {
                    if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                    {
                        return "darwin";
                    }
                    else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                    {
                        return "linux";
                    }
                    else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    {
                        return RuntimeOperatingSystems.Windows;
                    }

                    return null;
                }
            }

            public string OperatingSystemVersion
            {
                get
                {
                    if (OperatingSystem != RuntimeOperatingSystems.Windows)
                    {
                        return null;
                    }

                    if (_osVersion == null)
                    {
                        _osVersion = GetVersion().ToString();
                    }

                    return _osVersion;
                }
            }

            public string RuntimeType { get; private set; }

            public string RuntimeArchitecture { get; private set; }

            public string RuntimeVersion
            {
                get
                {
                    return "1.0.0-random";
                }
            }
            public static Version OSVersion
            {
                get
                {
                    uint dwVersion = GetVersion();

                    int major = (int)(dwVersion & 0xFF);
                    int minor = (int)((dwVersion >> 8) & 0xFF);

                    return new Version(major, minor);
                }
            }

            public string RuntimePath
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            private static uint GetVersion()
            {
                try
                {
                    return GetVersion_ApiSet();
                }
                catch
                {
                    try
                    {
                        return GetVersion_Kernel32();
                    }
                    catch
                    {
                        return 0;
                    }
                }

            }

            // The API set is required by OneCore based systems
            // and it is available only on Win 8 and newer
            [DllImport("api-ms-win-core-sysinfo-l1-2-1", EntryPoint = "GetVersion")]
            private static extern uint GetVersion_ApiSet();

            // For Win 7 and Win 2008 compatibility
            [DllImport("kernel32.dll", EntryPoint = "GetVersion")]
            private static extern uint GetVersion_Kernel32();
        }
#endif

    }
}
