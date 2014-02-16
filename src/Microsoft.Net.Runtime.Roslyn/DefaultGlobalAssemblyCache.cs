using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class DefaultGlobalAssemblyCache : IGlobalAssemblyCache
    {
        public bool TryResolvePartialName(string name, out string assemblyLocation)
        {
#if NET45
            return GlobalAssemblyCache.ResolvePartialName(name, out assemblyLocation) != null;
#else
            assemblyLocation = null;
            return false;
#endif
        }

        public bool Contains(string name)
        {
            string assemblyLocation;
            return TryResolvePartialName(name, out assemblyLocation);
        }

        public bool IsInGac(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            return Contains(name);
        }
    }
}
