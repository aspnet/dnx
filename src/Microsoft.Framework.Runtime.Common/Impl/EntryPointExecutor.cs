using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime.Common
{
    internal static class EntryPointExecutor
    {
        public static async Task<int> Execute(Assembly assembly, string[] args, IServiceProvider serviceProvider)
        {
            string name = assembly.GetName().Name;

            var programType = assembly.GetType("Program") ?? assembly.GetType(name + ".Program");

            if (programType == null)
            {
                var programTypeInfo = assembly.DefinedTypes.FirstOrDefault(t => t.Name == "Program");

                if (programTypeInfo == null)
                {
                    System.Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                    return -1;
                }

                programType = programTypeInfo.AsType();
            }

            var main = programType.GetTypeInfo().GetDeclaredMethods("Main").FirstOrDefault();

            if (main == null)
            {
                System.Console.WriteLine("'{0}' does not contain a 'Main' method suitable for an entry point", name);
                return -1;
            }

            object instance = programType.GetTypeInfo().IsAbstract ? null : ActivatorUtilities.CreateInstance(serviceProvider, programType);

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

            if (result is Task<int>)
            {
                return await (Task<int>)result;
            }

            if (result is Task)
            {
                await (Task)result;
                return 0;
            }

            return 0;
        }
    }
}
