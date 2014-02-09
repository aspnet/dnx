using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Common;
using Microsoft.Net.Runtime.Common.CommandLine;

namespace Microsoft.Net.ApplicationHost
{
    public class Program
    {
        private static readonly Dictionary<string, CommandOptionType> _options = new Dictionary<string, CommandOptionType>
        {
            { "framework", CommandOptionType.SingleValue },
            { "nobin", CommandOptionType.NoValue },
            { "watch", CommandOptionType.NoValue }
        };

        private readonly IHostContainer _container;

        public Program(IHostContainer container)
        {
            _container = container;
        }

        public async Task<int> Main(string[] args)
        {
            DefaultHostOptions options;
            string[] programArgs;

            ParseArgs(args, out options, out programArgs);

            try
            {
                var host = new DefaultHost(options);

                using (_container.AddHost(host))
                {
                    return await ExecuteMain(host, programArgs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, ExceptionHelper.GetExceptions(ex)));
            }

            return -2;
        }

        private void ParseArgs(string[] args, out DefaultHostOptions defaultHostOptions, out string[] outArgs)
        {
            var parser = new CommandLineParser();
            CommandOptions options;
            parser.ParseOptions(args, _options, out options);

            defaultHostOptions = new DefaultHostOptions();
            defaultHostOptions.UseCachedCompilations = !options.HasOption("nobin");
            defaultHostOptions.WatchFiles = options.HasOption("watch");
            defaultHostOptions.TargetFramework = Environment.GetEnvironmentVariable("TARGET_FRAMEWORK") ?? options.GetValue("framework");

#if NET45
            defaultHostOptions.ApplicationBaseDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
#else
            defaultHostOptions.ApplicationBaseDirectory = (string)typeof(AppDomain).GetRuntimeMethod("GetData", new[] { typeof(string) }).Invoke(AppDomain.CurrentDomain, new object[] { "APPBASE" });
#endif
            if (options.RemainingArgs.Count > 0)
            {
                defaultHostOptions.ApplicationName = options.RemainingArgs[0];

                outArgs = options.RemainingArgs.Skip(1).ToArray();
            }
            else
            {
                outArgs = options.RemainingArgs.ToArray();
            }
        }

        private async Task<int> ExecuteMain(DefaultHost host, string[] args)
        {
            var assembly = host.GetEntryPoint();

            if (assembly == null)
            {
                return -1;
            }

            return await EntryPointExecutor.Execute(assembly, args, pi => _container);
        }
    }
}
