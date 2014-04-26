using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Net.Runtime;

namespace klr.host
{
    public class HostContainer : IHostContainer
    {
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
            foreach (var host in _hosts.Reverse())
            {
                var assembly = host.Load(name);
                if (assembly != null)
                {
                    return assembly;
                }
            }

            return null;
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
