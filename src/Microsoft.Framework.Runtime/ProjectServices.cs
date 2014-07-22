using System;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime
{
    public class ProjectServices
    {
        public ProjectServices(TypeInformation loader,
                               TypeInformation builder,
                               TypeInformation metadataProvider,
                               TypeInformation libraryExporter)
        {
            Loader = loader;
            Builder = builder;
            MetadataProvider = metadataProvider;
            LibraryExportProvider = libraryExporter;
        }

        public TypeInformation Loader { get; private set; }
        public TypeInformation Builder { get; private set; }
        public TypeInformation MetadataProvider { get; private set; }
        public TypeInformation LibraryExportProvider { get; private set; }

        public static T CreateService<T>(IServiceProvider sp, TypeInformation typeInfo)
        {
            var assembly = Assembly.Load(new AssemblyName(typeInfo.AssemblyName));

            var type = assembly.GetType(typeInfo.TypeName);

            return (T)ActivatorUtilities.CreateInstance(sp, type);
        }
    }
}