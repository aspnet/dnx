using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
#if ASPNETCORE50
using System.Runtime.Loader;
#endif

namespace Microsoft.Framework.Runtime.Loader
{
#if ASPNETCORE50
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

        private Dictionary<Assembly, IAssemblyLoadContext> _cache = new Dictionary<Assembly, IAssemblyLoadContext>();

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

        public IAssemblyLoadContext GetLoadContext(Assembly assembly)
        {
            lock (_lockObj)
            {
                IAssemblyLoadContext context;
                if (_cache.TryGetValue(assembly, out context))
                {
                    return context;
                }
            }

            return LoadContext.Default;
        }

        public void SetLoadContext(Assembly assembly, IAssemblyLoadContext loadContext)
        {
            lock (_lockObj)
            {
                _cache[assembly] = loadContext;
            }
        }
    }
#endif
}