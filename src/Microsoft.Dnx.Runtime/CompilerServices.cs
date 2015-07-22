using System;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

namespace Microsoft.Dnx.Runtime
{
    public class CompilerServices
    {
        public CompilerServices(string name, TypeInformation compiler)
        {
            Name = name;
            ProjectCompiler = compiler;
        }

        public string Name { get; private set; }

        public TypeInformation ProjectCompiler { get; private set; }

        public static T CreateService<T>(IServiceProvider sp, IAssemblyLoadContext loadContext, TypeInformation typeInfo)
        {
            var assembly = loadContext.Load(typeInfo.AssemblyName);

            var type = assembly.GetType(typeInfo.TypeName);

            return (T)ActivatorUtilities.CreateInstance(sp, type);
        }
    }
}