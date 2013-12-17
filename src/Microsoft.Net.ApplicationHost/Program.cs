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
            string application;
            string[] arguments;

            if (args.Length >= 1)
            {
                application = args[0];
                arguments = args.Skip(1).ToArray();
            }
            else
            {
                application = Directory.GetCurrentDirectory();
                arguments = args;
            }

            try
            {
                var host = new DefaultHost(application);
                using (_container.AddHost(host))
                {
                    ExecuteMain(host, arguments);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
                return -2;
            }

            return 0;
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

        private void ExecuteMain(DefaultHost host, string[] args)
        {
            var assembly = host.GetEntryPoint();

            if (assembly == null)
            {
                return;
            }

            string name = assembly.GetName().Name;

            var program = assembly.GetType("Program");

            if (program == null)
            {
                var programTypeInfo = assembly.DefinedTypes.FirstOrDefault(t => t.Name == "Program");

                if (programTypeInfo == null)
                {
                    Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                    return;
                }

                program = programTypeInfo.AsType();
            }

            var main = program.GetTypeInfo().GetDeclaredMethods("Main").FirstOrDefault();

            if (main == null)
            {
                Console.WriteLine("'{0}' does not contain a 'Main' method suitable for an entry point", name);
                return;
            }

            object instance = null;
            if ((main.Attributes & MethodAttributes.Static) != MethodAttributes.Static)
            {
                var constructors = program.GetTypeInfo().DeclaredConstructors.Where(c => c.IsPublic).ToList();

                switch (constructors.Count)
                {
                    case 0:
                        Console.WriteLine("'{0}' does not contain a public constructor.", name);
                        return;

                    case 1:
                        var constructor = constructors[0];
                        var services = constructor.GetParameters().Select(pi => _container);
                        instance = constructor.Invoke(services.ToArray());
                        break;

                    default:
                        Console.WriteLine("'{0}' has too many public constructors for an entry point.", name);
                        return;
                }
            }

            var parameters = main.GetParameters();
            if (parameters.Length == 0)
            {
                main.Invoke(instance, null);
            }
            else if (parameters.Length == 1)
            {
                main.Invoke(instance, new object[] { args });
            }
        }
    }
}
