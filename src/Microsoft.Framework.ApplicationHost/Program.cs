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
        private static readonly Dictionary<string, CommandOptionType> _options = new Dictionary<string, CommandOptionType>
        {
            { "nobin", CommandOptionType.NoValue },
            { "watch", CommandOptionType.NoValue },
            { "packages", CommandOptionType.SingleValue},
        };

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

            ParseArgs(args, out options, out programArgs);

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


        private void ParseArgs(string[] args, out DefaultHostOptions defaultHostOptions, out string[] outArgs)
        {
            var parser = new CommandLineParser();
            CommandOptions options;
            parser.ParseOptions(args, _options, out options);

            defaultHostOptions = new DefaultHostOptions();
            defaultHostOptions.UseCachedCompilations = !options.HasOption("nobin");
            defaultHostOptions.WatchFiles = options.HasOption("watch");
            defaultHostOptions.PackageDirectory = options.GetValue("packages");

            defaultHostOptions.TargetFramework = _environment.TargetFramework;
            defaultHostOptions.ApplicationBaseDirectory = _environment.ApplicationBasePath;

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

        private Task<int> ExecuteMain(DefaultHost host, string applicationName, string[] args)
        {
            var assembly = host.GetEntryPoint(applicationName);

            if (assembly == null)
            {
                return Task.FromResult(-1);
            }

            return EntryPointExecutor.Execute(assembly, args, host.ServiceProvider);
        }
    }
}
