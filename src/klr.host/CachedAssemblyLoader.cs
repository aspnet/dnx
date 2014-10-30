using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Loader;

namespace klr.host
{
    public class CachedAssemblyLoader : IAssemblyLoader
    {
        private readonly ConcurrentDictionary<string, Assembly> _assemblyCache;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public CachedAssemblyLoader(ConcurrentDictionary<string, Assembly> assemblyCache)
        {
            _assemblyCache = assemblyCache;
            _loadContextAccessor = LoadContextAccessor.Instance;
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