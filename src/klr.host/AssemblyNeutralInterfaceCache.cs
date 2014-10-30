using System;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Framework.Runtime;

namespace klr.host
{
    public class AssemblyNeutralInterfaceCache : IAssemblyNeutralInterfaceCache
    {
        private readonly ConcurrentDictionary<string, Assembly> _cache;

        public AssemblyNeutralInterfaceCache(ConcurrentDictionary<string, Assembly> cache)
        {
            _cache = cache;
        }

        public Assembly GetAssembly(string name)
        {
            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return assembly;
            }
            return null;
        }

        public bool IsLoaded(string name)
        {
            return _cache.ContainsKey(name);
        }

        public void AddAssembly(string name, Assembly assembly)
        {
            _cache.TryAdd(name, assembly);
        }
    }
}