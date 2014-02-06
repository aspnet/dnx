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
    }
}
