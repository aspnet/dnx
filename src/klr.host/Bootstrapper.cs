
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;

namespace klr.host
{
    public class Bootstrapper
    {
        private readonly IHostContainer _container;

        public Bootstrapper(IHostContainer container)
        {
            _container = container;
        }

        public int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{app} [args]");
                return -1;
            }

            string name = args[0];

            return ExecuteMain(name, args.Skip(1).ToArray());
        }

        private int ExecuteMain(string name, string[] args)
        {
            var assembly = _container.Load(name);

            if (assembly == null)
            {
                return -1;
            }

            var programType = assembly.GetType("Program") ?? assembly.DefinedTypes.Where(t => t.Name == "Program").Select(t => t.AsType()).FirstOrDefault();

            if (programType == null)
            {
                Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                return -1;
            }

            // Invoke the constructor with the most arguments
            var ctor = programType.GetTypeInfo()
                                  .DeclaredConstructors
                                  .OrderByDescending(p => p.GetParameters().Length)
                                  .FirstOrDefault();

            var parameterValues = ctor.GetParameters()
                                      .Select(Satisfy)
                                      .ToArray();

            object programInstance = ctor.Invoke(parameterValues);

            var main = programType.GetTypeInfo().GetDeclaredMethods("Main").FirstOrDefault();

            if (main == null)
            {
                Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                return -1;
            }

            var parameters = main.GetParameters();
            object result = null;

            if (parameters.Length == 0)
            {
                result = main.Invoke(programInstance, null);
            }
            else if (parameters.Length == 1)
            {
                result = main.Invoke(programInstance, new object[] { args });
            }

            if (result is int)
            {
                return (int)result;
            }

            return 0;
        }

        private object Satisfy(ParameterInfo arg)
        {
            if (arg.ParameterType == typeof(IHostContainer))
            {
                return _container;
            }

            return null;
        }
    }
}