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

        public void Main(string[] args)
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

        private void ExecuteMain(DefaultHost host, string[] args)
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

            var main = program.GetMethod("Main", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (main == null)
            {
                Console.WriteLine("'{0}' does not contain a 'Main' method suitable for an entry point", name);
                return;
            }

            object instance = null;
            if ((main.Attributes & MethodAttributes.Static) != MethodAttributes.Static)
            {
                var constructorInfos = program.GetConstructors();
                if (constructorInfos == null || constructorInfos.Length == 0)
                {
                    instance = Activator.CreateInstance(program);
                }
                else if (constructorInfos.Length == 1)
                {
                    var services = constructorInfos[0].GetParameters().Select(pi => _container);
                    instance = constructorInfos[0].Invoke(services.ToArray());
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
