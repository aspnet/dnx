using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.Net.Runtime.Common
{
    public static class EntryPointExecutor
    {
        public static int Execute(Assembly assembly, string[] args, Func<ParameterInfo, object> parameterResolver)
        {
            string name = assembly.GetName().Name;

            var program = assembly.GetType("Program") ?? assembly.GetType(name + ".Program");

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
                        var services = constructor.GetParameters().Select(parameterResolver);
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
