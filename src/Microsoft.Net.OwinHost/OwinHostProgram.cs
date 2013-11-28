using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Loader;
using Microsoft.Owin.Hosting.Services;
using Microsoft.Owin.Hosting.Starter;
using Microsoft.Owin.Hosting.Tracing;
using Microsoft.Owin.Hosting.Utilities;
using OwinHost.Options;

namespace Microsoft.Net.OwinHost
{
    public class Program
    {
        private readonly IHostContainer _hostContainer;

        public Program(IHostContainer hostContainer)
        {
            _hostContainer = hostContainer;
        }

        public int Main(string[] args)
        {
            Command command;
            try
            {
                command = CreateCommandModel().Parse(args);
            }
            catch (Exception e)
            {
                if (e is CommandException || e is MissingMethodException || e is EntryPointNotFoundException)
                {
                    // these exception types are basic message errors
                    Console.WriteLine(Resources.ProgramOutput_CommandLineError, e.Message);
                    Console.WriteLine();
                    ShowHelp(new Command { Model = CreateCommandModel() });
                }
                else
                {
                    // otherwise let the exception terminate the process
                    throw;
                }
                return 1;
            }

            if (command.Run())
            {
                return 0;
            }
            return 1;

            //string path = Directory.GetCurrentDirectory();
            //string url = "http://localhost:8080/";

            //var host = new HostStarter();

            //try
            //{
            //    var options = new StartOptions();
            //    options.Urls.Add(url);

            //    var context = new StartContext(options);

            //    IServiceProvider services = ServicesFactory.Create(context.Options.Settings, sp =>
            //    {
            //        sp.AddInstance<IAppLoader>(new MyAppLoader(_host, assembly));
            //    });

            //    Console.ReadLine();
            //}
            //catch(Exception ex)
            //{
            //    Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
            //    Environment.Exit(-1);
            //}
        }

        public CommandModel CreateCommandModel()
        {
            var model = new CommandModel();

            // run this alternate command for any help-like parameter
            model.Command("{show help}", IsHelpOption, (m, v) => { }).Execute(ShowHelp);

            // otherwise use these switches
            model.Option<StartOptions, string>(
                "server", "s", Resources.ProgramOutput_ServerOption,
                (options, value) => options.ServerFactory = value);

            model.Option<StartOptions, string>(
                "url", "u", Resources.ProgramOutput_UriOption,
                (options, value) => options.Urls.Add(value));

            model.Option<StartOptions, int>(
                "port", "p", Resources.ProgramOutput_PortOption,
                (options, value) => options.Port = value);

            model.Option<StartOptions, string>(
                "directory", "d", Resources.ProgramOutput_DirectoryOption,
                (options, value) => options.Settings["directory"] = value);

            model.Option<StartOptions, string>(
                "traceoutput", "o", Resources.ProgramOutput_OutputOption,
                (options, value) => options.Settings["traceoutput"] = value);

            model.Option<StartOptions, string>(
                "settings", Resources.ProgramOutput_SettingsOption,
                LoadSettings);

            model.Option<StartOptions, string>(
                "boot", "b", Resources.ProgramOutput_BootOption,
                (options, value) => options.Settings["boot"] = value);

            model.Option<StartOptions, int>(
                "devMode", "dev", Resources.ProgramOutput_DevModeOption,
                (options, value) => options.Settings["devMode"] = value.ToString());

            /* Disabled until we need to consume it anywhere.
            model.Option<StartOptions, string>(
                "verbosity", "v", "Set output verbosity level.",
                (options, value) => options.Settings["traceverbosity"] = value);
            */
            // and take the name of the application startup

            model.Parameter<string>((cmd, value) =>
            {
                var options = cmd.Get<StartOptions>();
                if (options.AppStartup == null)
                {
                    options.AppStartup = value;
                }
                else
                {
                    options.AppStartup += " " + value;
                }
            });

            // to call this action

            model.Execute<StartOptions>(RunServer);

            return model;
        }

        private static void WriteLine(string data)
        {
            Console.WriteLine(data);
        }

        public void RunServer(StartOptions options)
        {
            if (options == null)
            {
                return;
            }

            // get existing loader factory services
            string appLoaderFactories;
            if (!options.Settings.TryGetValue(typeof(IAppLoaderFactory).FullName, out appLoaderFactories) ||
                !string.IsNullOrEmpty(appLoaderFactories))
            {
                // use the built-in AppLoaderFactory as the default
                appLoaderFactories = typeof(AppLoaderFactory).AssemblyQualifiedName;
            }

            // prepend with our app loader factory 
            options.Settings[typeof(IAppLoaderFactory).FullName] =
                typeof(AppLoaderWrapper).AssemblyQualifiedName + ";" + appLoaderFactories;

            var host = new DefaultHost(AppDomain.CurrentDomain.SetupInformation.ApplicationBase);

            if (options.Settings.ContainsKey("devMode"))
            {
                host.OnChanged += () =>
                {
                    Environment.Exit(250);
                };
            }

            using (_hostContainer.AddHost(host))
            {
                WriteLine("Starting with " + GetDisplayUrl(options));

                // Ensure the DefaultHost is available to the AppLoaderWrapper
                IServiceProvider container = ServicesFactory.Create(options.Settings, services =>
                {
                    services.Add(typeof(ITraceOutputFactory), () => new NoopTraceOutputFactory());
                    services.Add(typeof(DefaultHost), () => host);
                });

                IHostingStarter starter = container.GetService<IHostingStarter>();
                using (starter.Start(options))
                {
                    WriteLine("Started successfully");

                    WriteLine("Press Enter to exit");
                    Console.ReadLine();

                    WriteLine("Terminating.");
                }
            }
        }

        private static string GetDisplayUrl(StartOptions options)
        {
            string url = null;
            if (options.Urls.Count > 0)
            {
                url = "urls: " + string.Join(", ", options.Urls);
            }
            else if (options.Port.HasValue)
            {
                string port = options.Port.Value.ToString(CultureInfo.InvariantCulture);
                url = "port: " + port + " (http://localhost:" + port + "/)";
            }
            return url ?? "the default port: 5000 (http://localhost:5000/)";
        }

        private static bool IsHelpOption(string s)
        {
            var helpOptions = new[] { "-?", "-h", "--help" };
            return helpOptions.Contains(s, StringComparer.OrdinalIgnoreCase);
        }

        private static void LoadSettings(StartOptions options, string settingsFile)
        {
            SettingsLoader.LoadFromSettingsFile(settingsFile, options.Settings);
        }

        private static void ShowHelp(Command cmd)
        {
            CommandModel rootCommand = cmd.Model.Root;

            string usagePattern = "OwinHost";
            foreach (var option in rootCommand.Options)
            {
                if (String.IsNullOrEmpty(option.ShortName))
                {
                    usagePattern += " [--" + option.Name + " VALUE]";
                }
                else
                {
                    usagePattern += " [-" + option.ShortName + " " + option.Name + "]";
                }
            }
            usagePattern += " [AppStartup]";

            Console.WriteLine(Resources.ProgramOutput_Intro);
            Console.WriteLine();
            Console.WriteLine(FormatLines(Resources.ProgramOutput_Usage, usagePattern, 0, 15));
            Console.WriteLine();
            Console.WriteLine(Resources.ProgramOutput_Options);

            foreach (var option in rootCommand.Options)
            {
                string header;
                if (string.IsNullOrWhiteSpace(option.ShortName))
                {
                    header = "  --" + option.Name;
                }
                else
                {
                    header = "  -" + option.ShortName + ",--" + option.Name;
                }
                Console.WriteLine(FormatLines(header, option.Description, 20, 2));
            }

            Console.WriteLine();
            Console.WriteLine(Resources.ProgramOutput_ParametersHeader);
            Console.WriteLine(FormatLines("  AppStartup", Resources.ProgramOutput_AppStartupParameter, 20, 2));
            Console.WriteLine();
            Console.WriteLine(FormatLines(string.Empty, Resources.ProgramOutput_AppStartupDescription, 2, 2));

            Console.WriteLine();
            Console.WriteLine(Resources.ProgramOutput_EnvironmentVariablesHeader);
            Console.WriteLine(FormatLines("  PORT", Resources.ProgramOutput_PortEnvironmentDescription, 20, 2));
            Console.WriteLine(FormatLines("  OWIN_SERVER", Resources.ProgramOutput_ServerEnvironmentDescription, 20, 2));
            Console.WriteLine();
            Console.WriteLine(Resources.ProgramOutput_Example);
            Console.WriteLine();
        }

        public static string FormatLines(string header, string body, int bodyOffset, int hangingIndent)
        {
            if (header == null)
            {
                throw new ArgumentNullException("header");
            }

            string total = string.Empty;
            int lineLimit = Console.WindowWidth - 2;
            int offset = Math.Max(header.Length + 2, bodyOffset);

            string line = header;

            while (offset + body.Length > lineLimit)
            {
                int bodyBreak = body.LastIndexOf(' ', lineLimit - offset);
                if (bodyBreak == -1)
                {
                    break;
                }
                total += line + new string(' ', offset - line.Length) + body.Substring(0, bodyBreak) + Environment.NewLine;
                offset = bodyOffset + hangingIndent;
                line = string.Empty;
                body = body.Substring(bodyBreak + 1);
            }
            return total + line + new string(' ', offset - line.Length) + body;
        }

        private static IEnumerable<string> GetExceptions(Exception ex)
        {
            if (ex.InnerException != null)
            {
                foreach (var e in GetExceptions(ex.InnerException))
                {
                    yield return e;
                }
            }

            if (!(ex is TargetInvocationException))
            {
                yield return ex.Message;
            }
        }
    }
}
