using System;
using System.Reflection;
using System.Runtime.Hosting.Loader;

namespace klr.core45.managed
{
    public class DelegateAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly Func<AssemblyName, Assembly> _callback;

        public DelegateAssemblyLoadContext(Func<AssemblyName, Assembly> callback)
        {
            _callback = callback;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return _callback(assemblyName);
        }
    }
}
