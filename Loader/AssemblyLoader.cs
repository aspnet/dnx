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

        public Assembly Load(LoadOptions options)
        {
            var sw = new Stopwatch();
            sw.Start();
            Trace.TraceInformation("Loading {0}", options.AssemblyName);

            Assembly asm;

            if (!_cache.TryGetValue(options.AssemblyName, out asm))
            {
                asm = LoadImpl(options, sw);

                if (asm != null)
                {
                    _cache.TryAdd(options.AssemblyName, asm);
                }
            }
            else
            {
                sw.Stop();
                Trace.TraceInformation("[Cache]: Loaded {0} in {1}ms", options.AssemblyName, sw.ElapsedMilliseconds);
            }

            return asm;
        }

        public void Attach(AppDomain appDomain)
        {
            appDomain.AssemblyResolve += OnAssemblyResolve;
        }

        public void Detach(AppDomain appDomain)
        {
            appDomain.AssemblyResolve -= OnAssemblyResolve;
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var an = new AssemblyName(args.Name);

            var options = new LoadOptions
            {
                AssemblyName = an.Name,
                Version = an.Version != null ? an.Version.ToString() : null
            };

            return Load(options);
        }

        private Assembly LoadImpl(LoadOptions options, Stopwatch sw)
        {
            foreach (var loader in _loaders)
            {
                var assembly = loader.Load(options);

                if (assembly != null)
                {
                    sw.Stop();

                    Trace.TraceInformation("[{0}]: Finished loading {1} in {2}ms", loader.GetType().Name, options.AssemblyName, sw.ElapsedMilliseconds);

                    return assembly;
                }
            }

            return null;
        }
    }
}
