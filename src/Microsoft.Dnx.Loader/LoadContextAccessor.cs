using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
#if DNXCORE50
using System.Runtime.Loader;
#endif

namespace Microsoft.Dnx.Runtime.Loader
{
#if DNXCORE50
    public class LoadContextAccessor : IAssemblyLoadContextAccessor
    {
        private static readonly LoadContextAccessor _instance = new LoadContextAccessor();

        public static LoadContextAccessor Instance
        {
            get
            {
                return _instance;
            }
        }

        public IAssemblyLoadContext GetLoadContext(Assembly assembly)
        {
            return (IAssemblyLoadContext)AssemblyLoadContext.GetLoadContext(assembly);
        }

        public IAssemblyLoadContext Default
        {
            get
            {
                return (IAssemblyLoadContext)AssemblyLoadContext.Default;
            }
        }
    }
#else
    public class LoadContextAccessor : IAssemblyLoadContextAccessor
    {
        private static readonly LoadContextAccessor _instance = new LoadContextAccessor();

        private Dictionary<Assembly, LoadContext> _cache = new Dictionary<Assembly, LoadContext>();

        private readonly object _lockObj = new object();

        public static LoadContextAccessor Instance
        {
            get
            {
                return _instance;
            }
        }

        public IAssemblyLoadContext Default
        {
            get
            {
                return LoadContext.Default;
            }
        }

        IAssemblyLoadContext IAssemblyLoadContextAccessor.GetLoadContext(Assembly assembly)
        {
            return GetLoadContext(assembly);
        }

        public LoadContext GetLoadContext(Assembly assembly)
        {
            lock (_lockObj)
            {
                LoadContext context;
                if (_cache.TryGetValue(assembly, out context))
                {
                    return context;
                }
            }

            return LoadContext.Default;
        }

        public void SetLoadContext(Assembly assembly, LoadContext loadContext)
        {
            lock (_lockObj)
            {
                _cache[assembly] = loadContext;
            }
        }
    }
#endif
}
