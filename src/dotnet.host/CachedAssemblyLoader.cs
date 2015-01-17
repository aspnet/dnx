using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Framework.Runtime;

namespace dotnet.host
{
    public class CachedAssemblyLoader : IAssemblyLoader
    {
        private readonly ConcurrentDictionary<string, Assembly> _assemblyCache;

        public CachedAssemblyLoader(ConcurrentDictionary<string, Assembly> assemblyCache)
        {
            _assemblyCache = assemblyCache;
        }

        public Assembly Load(string name)
        {
            Assembly assembly;
            if (_assemblyCache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            return null;
        }
    }
}