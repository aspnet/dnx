using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;

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
                Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
                return -2;
            }
        }

        private void ParseArgs(string[] args, out DefaultHostOptions options, out string[] outArgs)
        {
            options = new DefaultHostOptions();
            options.ProjectDir = Directory.GetCurrentDirectory();

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
                    options.ProjectDir = arg;
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
                yield return ex.ToString();
            }
        }

        private int ExecuteMain(DefaultHost host, string[] args)
        {
            var assembly = host.GetEntryPoint();

            if (assembly == null)
            {
                return -1;
            }

            string name = assembly.GetName().Name;

            var program = assembly.GetType("Program");

            if (program == null)
            {
                var programTypeInfo = assembly.DefinedTypes.FirstOrDefault(t => t.Name == "Program");

                if (programTypeInfo == null)
                {
                    Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                    return -1;
                }

                program = programTypeInfo.AsType();
            }

            var main = program.GetTypeInfo().GetDeclaredMethods("Main").FirstOrDefault();

            if (main == null)
            {
                Console.WriteLine("'{0}' does not contain a 'Main' method suitable for an entry point", name);
                return -1;
            }

            object instance = null;
            if ((main.Attributes & MethodAttributes.Static) != MethodAttributes.Static)
            {
                var constructors = program.GetTypeInfo().DeclaredConstructors.Where(c => c.IsPublic).ToList();

                switch (constructors.Count)
                {
                    case 0:
                        Console.WriteLine("'{0}' does not contain a public constructor.", name);
                        return -1;

                    case 1:
                        var constructor = constructors[0];
                        var services = constructor.GetParameters().Select(pi => _container);
                        instance = constructor.Invoke(services.ToArray());
                        break;

                    default:
                        Console.WriteLine("'{0}' has too many public constructors for an entry point.", name);
                        return -1;
                }
            }

            object result = null;
            var parameters = main.GetParameters();

            if (parameters.Length == 0)
            {
                result = main.Invoke(instance, null);
            }
            else if (parameters.Length == 1)
            {
                result = main.Invoke(instance, new object[] { args });
            }

            if (result is int)
            {
                return (int)result;
            }

            return 0;
        }
    }
}
