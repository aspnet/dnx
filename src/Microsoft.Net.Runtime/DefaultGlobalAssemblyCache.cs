using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime
{
    public class DefaultGlobalAssemblyCache : IGlobalAssemblyCache
    {
        public bool TryResolvePartialName(string name, out string assemblyLocation)
        {
#if DESKTOP
            return GlobalAssemblyCache.ResolvePartialName(name, out assemblyLocation) != null;
#else
            assemblyLocation = null;
            return false;
#endif
        }
    }
}
