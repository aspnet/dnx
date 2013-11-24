using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;

namespace K
{
    public class Program
    {
        private readonly IHostContainer _container;

        public Program(IHostContainer container)
        {
            _container = container;
        }

        public void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("k [command] [application]");
                Environment.Exit(-1);
                return;
            }

            string command = args[0];
            string application = args.Length >= 2 ? args[1] : Directory.GetCurrentDirectory();

            var host = new DefaultHost(application);
            IDisposable scope = _container.AddHost(host);

            try
            {
                if (command.Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    using (scope)
                    {
                        host.Build();
                    }
                }
                else if (command.Equals("clean", StringComparison.OrdinalIgnoreCase))
                {
                    using (scope)
                    {
                        host.Clean();
                    }
                }
                else
                {
                    Console.WriteLine("unknown command '{0}'", command);
                    Environment.Exit(-1);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
                Environment.Exit(-2);
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
                yield return ex.Message;
            }
        }

        private static void ExecuteMain(DefaultHost host, string path, string[] args)
        {
            var assembly = host.GetEntryPoint();

            if (assembly == null)
            {
                return;
            }

            string name = assembly.GetName().Name;

            var program = assembly.GetType("Program") ?? assembly.GetTypes().FirstOrDefault(t => t.Name == "Program");

            if (program == null)
            {
                Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                return;
            }

            var main = program.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (main == null)
            {
                Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                return;
            }

            var parameters = main.GetParameters();
            if (parameters.Length == 0)
            {
                main.Invoke(null, null);
            }
            else if (parameters.Length == 1)
            {
                main.Invoke(null, new object[] { args });
            }
        }
    }
}
