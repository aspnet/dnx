
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Common;

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

            var name = args[0];
            var programArgs = args.Skip(1).ToArray();

            var assembly = Assembly.Load(new AssemblyName(name));

            if (assembly == null)
            {
                return -1;
            }

            return EntryPointExecutor.Execute(assembly, programArgs, Satisfy);
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