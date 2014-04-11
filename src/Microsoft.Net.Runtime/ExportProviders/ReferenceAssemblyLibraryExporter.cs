using System.Runtime.Versioning;
using Microsoft.Net.Runtime.Loader;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class ReferenceAssemblyLibraryExporter : ILibraryExportProvider
    {
        public ReferenceAssemblyLibraryExporter()
        {
            FrameworkResolver = new FrameworkReferenceResolver();
        }

        public FrameworkReferenceResolver FrameworkResolver { get; private set; }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            string path;
            if (FrameworkResolver.TryGetAssembly(name, targetFramework, out path))
            {
                return new LibraryExport(path);
            }

            return null;
        }
    }
}
