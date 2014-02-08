using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Common;

namespace Microsoft.Net.ApplicationHost
{
    public class Program
    {
        private readonly IHostContainer _container;

        public Program(IHostContainer container)
        {
            _container = container;
        }

        public int Main(string[] args)
        {
            DefaultHostOptions options;
            string[] programArgs;

            ParseArgs(args, out options, out programArgs);

            try
            {
                var host = new DefaultHost(options);

                using (_container.AddHost(host))
                {
                    return ExecuteMain(host, programArgs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, ExceptionHelper.GetExceptions(ex)));
                return -2;
            }
        }

        private void ParseArgs(string[] args, out DefaultHostOptions options, out string[] outArgs)
        {
            options = new DefaultHostOptions();

#if NET45
            options.ProjectDir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
#else
            options.ProjectDir = (string)typeof(AppDomain).GetRuntimeMethod("GetData", new[] { typeof(string) }).Invoke(AppDomain.CurrentDomain, new object[] { "APPBASE" });
#endif

            // TODO: Just pass this as an argument from the caller
            options.TargetFramework = Environment.GetEnvironmentVariable("TARGET_FRAMEWORK");

            int index = 0;
            for (index = 0; index < args.Length; index++)
            {
                string arg = args[index];
                if (arg.StartsWith("--"))
                {
                    var option = arg.Substring(2);

                    switch (option.ToLowerInvariant())
                    {
                        case "nobin":
                            options.UseCachedCompilations = false;
                            break;
                        case "framework":
                            options.TargetFramework = option;
                            break;
                        case "watch":
                            options.WatchFiles = true;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    options.ApplicationName = arg;
                    index++;
                    break;
                }
            }

            if (index == 0)
            {
                outArgs = args;
            }
            else
            {
                outArgs = args.Skip(index).ToArray();
            }
        }

        private int ExecuteMain(DefaultHost host, string[] args)
        {
            var assembly = host.GetEntryPoint();

            if (assembly == null)
            {
                return -1;
            }

            return EntryPointExecutor.Execute(assembly, args, pi => _container);
        }
    }
}
