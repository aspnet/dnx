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
            var assemblyLoadContext = AssemblyLoadContext.GetLoadContext(assembly);
            return assemblyLoadContext as IAssemblyLoadContext ?? new LoadContextWrapper(assemblyLoadContext);
        }

        public IAssemblyLoadContext Default
        {
            get
            {
                return new LoadContextWrapper(AssemblyLoadContext.Default);
            }
        }

        // This wrapper assumes there's public methods LoadFile and LoadStream on the
        // assembly load context implementation. This is the case for the DelegateAssemblyLoadContext
        // and all other implementations should implement this interface to play nicely in
        // this world.
        // The code that bootstraps the runtime can't depend on these interfaces
        private class LoadContextWrapper : IAssemblyLoadContext
        {
            private readonly AssemblyLoadContext _assemblyLoadContext;
            private readonly Func<string, Assembly> _loadFile;
            private readonly Func<Stream, Stream, Assembly> _loadStream;

            public LoadContextWrapper(AssemblyLoadContext assemblyLoadContext)
            {
                _assemblyLoadContext = assemblyLoadContext;

                var typeInfo = _assemblyLoadContext.GetType().GetTypeInfo();
                var loaderFileMethod = typeInfo.GetDeclaredMethod("LoadFile");
                var loadStreamMethod = typeInfo.GetDeclaredMethod("LoadStream");

                _loadFile = (Func<string, Assembly>)loaderFileMethod.CreateDelegate(typeof(Func<string, Assembly>), _assemblyLoadContext);
                _loadStream = (Func<Stream, Stream, Assembly>)loadStreamMethod.CreateDelegate(typeof(Func<Stream, Stream, Assembly>), _assemblyLoadContext);
            }

            public Assembly Load(string name)
            {
                return _assemblyLoadContext.LoadFromAssemblyName(new AssemblyName(name));
            }

            public Assembly LoadFile(string path)
            {
                return _loadFile(path);
            }

            public Assembly LoadStream(Stream assemblyStream, Stream assemblySymbols)
            {
                return _loadStream(assemblyStream, assemblySymbols);
            }

            public void Dispose()
            {

            }
        }
    }
#else
    public class LoadContextAccessor : IAssemblyLoadContextAccessor
    {
        private static readonly Lazy<LoadContextAccessor> _instance = new Lazy<LoadContextAccessor>();

        private Dictionary<Assembly, IAssemblyLoadContext> _cache = new Dictionary<Assembly, IAssemblyLoadContext>();

        public static LoadContextAccessor Instance
        {
            get
            {
                return _instance.Value;
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
            IAssemblyLoadContext context;
            if (_cache.TryGetValue(assembly, out context))
            {
                return context;
            }

            return LoadContext.Default;
        }

        public void SetLoadContext(Assembly assembly, IAssemblyLoadContext loadContext)
        {
            _cache[assembly] = loadContext;
        }
    }
#endif
}