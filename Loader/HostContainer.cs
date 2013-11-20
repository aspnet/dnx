using System;
using System.Collections.Generic;
using System.Reflection;

namespace Loader
{
    public class HostContainer : IHostContainer
    {
        private readonly Stack<IHost> _hosts = new Stack<IHost>();

        public IDisposable AddHost(IHost host)
        {
            _hosts.Push(host);

            return new DisposableAction(() =>
            {
                _hosts.Pop();
                host.Dispose();
            });
        }

        public Assembly GetEntryPoint()
        {
            foreach (var host in _hosts)
            {
                Assembly assembly = host.GetEntryPoint();
                if (assembly != null)
                {
                    return assembly;
                }
            }

            return null;
        }

        public Assembly Load(string name)
        {
            foreach (var host in _hosts)
            {
                Assembly assembly = host.Load(name);
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
