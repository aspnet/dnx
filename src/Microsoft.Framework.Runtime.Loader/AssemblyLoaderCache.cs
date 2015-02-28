using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    internal class AssemblyLoaderCache
    {
        private readonly ConcurrentDictionary<string, object> _assemblyLoadLocks = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Assembly> _assemblyCache = new ConcurrentDictionary<string, Assembly>(StringComparer.Ordinal);

        public Assembly GetOrAdd(string name, Func<string, Assembly> factory)
        {
            // If the assembly was already loaded use it
            Assembly assembly;
            if (_assemblyCache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            var loadLock = _assemblyLoadLocks.GetOrAdd(name, new object());

            try
            {
                // Concurrently loading the assembly might result in two distinct instances of the same assembly 
                // being loaded. This was observed when loading via Assembly.LoadStream. Prevent this by locking on the name.
                lock (loadLock)
                {
                    if (_assemblyCache.TryGetValue(name, out assembly))
                    {
                        // This would succeed in case the thread was previously waiting on the lock when assembly 
                        // load was in progress
                        return assembly;
                    }

                    assembly = factory(name);

                    if (assembly != null)
                    {
                        _assemblyCache[name] = assembly;
                    }
                }
            }
            finally
            {
                _assemblyLoadLocks.TryRemove(name, out loadLock);
            }

            return assembly;
        }
    }
}