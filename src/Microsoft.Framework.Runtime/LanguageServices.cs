using System;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime
{
    public class LanguageServices
    {
        public LanguageServices(string name, TypeInformation libraryExporter)
        {
            Name = name;
            LibraryExportProvider = libraryExporter;
        }

        public string Name { get; private set; }

        public TypeInformation LibraryExportProvider { get; private set; }

        public static T CreateService<T>(IServiceProvider sp, TypeInformation typeInfo)
        {
            var assembly = Assembly.Load(new AssemblyName(typeInfo.AssemblyName));

            var type = assembly.GetType(typeInfo.TypeName);

            return (T)ActivatorUtilities.CreateInstance(sp, type);
        }
    }
}