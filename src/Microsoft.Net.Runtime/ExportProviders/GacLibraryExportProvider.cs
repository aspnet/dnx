using System.Runtime.Versioning;
using Microsoft.Net.Runtime.Loader;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class GacLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IGlobalAssemblyCache _globalAssemblyCache;

        public GacLibraryExportProvider(IGlobalAssemblyCache globalAssemblyCache)
        {
            _globalAssemblyCache = globalAssemblyCache;
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            // Only use the GAC on full .NET
            if (VersionUtility.IsDesktop(targetFramework))
            {
                string assemblyLocation;
                if (_globalAssemblyCache.TryResolvePartialName(name, out assemblyLocation))
                {
                    return new LibraryExport(assemblyLocation);
                }
            }

            return null;
        }
    }
}
