using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Loader
{
    public class AssemblyLoader : IAssemblyLoader
    {
        private List<IAssemblyLoader> _loaders = new List<IAssemblyLoader>();
        private readonly ConcurrentDictionary<string, Assembly> _cache = new ConcurrentDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        public void Add(IAssemblyLoader loader)
        {
            _loaders.Add(loader);
        }

        public Assembly Load(string name)
        {
            var sw = new Stopwatch();
            sw.Start();
            Trace.TraceInformation("Loading {0}", name);

            Assembly asm;

            if (!_cache.TryGetValue(name, out asm))
            {
                asm = LoadImpl(name);

                sw.Stop();
                Trace.TraceInformation("Finished loading {0} in {1}ms", name, sw.ElapsedMilliseconds);

                if (asm != null)
                {
                    _cache.TryAdd(name, asm);
                }
            }
            else
            {
                sw.Stop();
                Trace.TraceInformation("Retrieved {0} from cache in {1}ms", name, sw.ElapsedMilliseconds);
            }

            return asm;
        }

        public void Attach(AppDomain appDomain)
        {
            appDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Load(new AssemblyName(args.Name).Name);
        }

        private Assembly LoadImpl(string name)
        {
            foreach (var loader in _loaders)
            {
                var assembly = loader.Load(name);

                if (assembly != null)
                {
                    return assembly;
                }
            }

            return null;
        }
    }
}
