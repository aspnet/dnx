using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Net.Runtime;

namespace klr.host
{
    public class HostContainer : IHostContainer
    {
        private readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>();
        private readonly Stack<IHost> _hosts = new Stack<IHost>();

        public IDisposable AddHost(IHost host)
        {
            _hosts.Push(host);

            return new DisposableAction(() =>
            {
                var removed = _hosts.Pop();
                if (!ReferenceEquals(host, removed))
                {
                    throw new InvalidOperationException("Host scopes being disposed in wrong order");
                }
                removed.Dispose();
            });
        }

        public Assembly Load(string name)
        {
            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            foreach (var host in _hosts)
            {
                assembly = host.Load(name);
                if (assembly != null)
                {
                    ExtractAssemblyNeutralInterfaces(assembly);

                    _cache[name] = assembly;
                    return assembly;
                }
            }

            return null;
        }

        private void ExtractAssemblyNeutralInterfaces(Assembly assembly)
        {
            // Embedded assemblies end with .dll
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(".dll"))
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(name);

                    if (_cache.ContainsKey(assemblyName))
                    {
                        continue;
                    }

                    var ms = new MemoryStream();
                    assembly.GetManifestResourceStream(name).CopyTo(ms);
                    _cache[assemblyName] = Assembly.Load(ms.ToArray());
                }
            }
        }

        private class DisposableAction : IDisposable
        {
            private readonly Action _action;
            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }
    }
}
