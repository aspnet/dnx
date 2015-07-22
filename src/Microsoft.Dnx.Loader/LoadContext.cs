using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
#if DNXCORE50
using System.Runtime.Loader;
#endif

namespace Microsoft.Dnx.Runtime.Loader
{
#if DNXCORE50
    public abstract class LoadContext : AssemblyLoadContext, IAssemblyLoadContext
    {
        private readonly AssemblyLoaderCache _cache = new AssemblyLoaderCache();

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return _cache.GetOrAdd(assemblyName, LoadAssembly);
        }

        Assembly IAssemblyLoadContext.Load(AssemblyName assemblyName)
        {
            return LoadFromAssemblyName(assemblyName);
        }

        public abstract Assembly LoadAssembly(AssemblyName assemblyName);

        public Assembly LoadFile(string path)
        {
            // Look for platform specific native image
            string nativeImagePath = GetNativeImagePath(path);

            if (nativeImagePath != null)
            {
                return LoadFromNativeImagePath(nativeImagePath, path);
            }

            return LoadFromAssemblyPath(path);
        }

        public Assembly LoadStream(Stream assembly, Stream assemblySymbols)
        {
            if (assemblySymbols == null)
            {
                return LoadFromStream(assembly);
            }

            return LoadFromStream(assembly, assemblySymbols);
        }

        public static void InitializeDefaultContext(LoadContext loadContext)
        {
            AssemblyLoadContext.InitializeDefaultContext(loadContext);
        }

        private string GetNativeImagePath(string ilPath)
        {
            var directory = Path.GetDirectoryName(ilPath);
            var arch = IntPtr.Size == 4 ? "x86" : "AMD64";

            var nativeImageName = Path.GetFileNameWithoutExtension(ilPath) + ".ni.dll";
            var nativePath = Path.Combine(directory, arch, nativeImageName);

            if (File.Exists(nativePath))
            {
                return nativePath;
            }
            else
            {
                // Runtime is arch sensitive so the ni is in the same folder as IL
                nativePath = Path.Combine(directory, nativeImageName);
                if (File.Exists(nativePath))
                {
                    return nativePath;
                }
            }

            return null;
        }

        public void Dispose()
        {

        }
    }
#else
    public abstract class LoadContext : IAssemblyLoadContext
    {
        private static readonly ConcurrentDictionary<string, LoadContext> _contexts = new ConcurrentDictionary<string, LoadContext>();

        internal static LoadContext Default;

        private readonly AssemblyLoaderCache _cache = new AssemblyLoaderCache();

        private string _contextId;

        static LoadContext()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        public LoadContext()
        {
            _contextId = Guid.NewGuid().ToString();

            _contexts.TryAdd(_contextId, this);
        }

        public static void InitializeDefaultContext(LoadContext loadContext)
        {
            var old = Interlocked.CompareExchange(ref Default, loadContext, null);
            if (old != null)
            {
                throw new InvalidOperationException("Default load context already set");
            }

            loadContext._contextId = null;
        }

        public void Dispose()
        {
            LoadContext context;
            _contexts.TryRemove(_contextId, out context);
            _contextId = null;
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            if (string.IsNullOrEmpty(_contextId))
            {
                return Assembly.Load(assemblyName);
            }

            var contextIdAssemblyName = (AssemblyName)assemblyName.Clone();
            contextIdAssemblyName.Name = _contextId + "$" + assemblyName.Name;

            return Assembly.Load(contextIdAssemblyName);
        }

        private Assembly LoadAssemblyImpl(AssemblyName assemblyName)
        {
            return _cache.GetOrAdd(assemblyName, LoadAssembly);
        }

        public abstract Assembly LoadAssembly(AssemblyName assemblyName);

        public Assembly LoadFile(string assemblyPath)
        {
            return Assembly.LoadFile(assemblyPath);
        }

        public Assembly LoadStream(Stream assembly, Stream assemblySymbols)
        {
            byte[] assemblyBytes = GetStreamAsByteArray(assembly);
            byte[] assemblySymbolBytes = null;

            if (assemblySymbols != null)
            {
                assemblySymbolBytes = GetStreamAsByteArray(assemblySymbols);
            }

            return Assembly.Load(assemblyBytes, assemblySymbolBytes);
        }

        private byte[] GetStreamAsByteArray(Stream stream)
        {
            // Fast path assuming the stream is a memory stream
            var ms = stream as MemoryStream;
            if (ms != null)
            {
                return ms.ToArray();
            }

            // Otherwise copy the bytes
            using (ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var domain = (AppDomain)sender;

            var afterPolicy = domain.ApplyPolicy(args.Name);

            if (afterPolicy != args.Name)
            {
                return Assembly.Load(afterPolicy);
            }

            // {context}${name}
            var assemblyName = new AssemblyName(args.Name);

            var parts = assemblyName.Name.Split('$');

            if (parts.Length == 2)
            {
                string contextId = parts[0];
                string shortName = parts[1];

                LoadContext context;
                if (_contexts.TryGetValue(contextId, out context))
                {
                    var shortAssemblyName = (AssemblyName)assemblyName.Clone();
                    shortAssemblyName.Name = shortName;

                    Assembly assembly;
                    if (TryLoadAssembly(context, shortAssemblyName, out assembly))
                    {
                        return assembly;
                    }
                }
            }

            // If we have a requesting assembly then try to infer the load context from it
            if (args.RequestingAssembly != null)
            {
                // Get the relevant load context for the requesting assembly
                var loadContext = LoadContextAccessor.Instance.GetLoadContext(args.RequestingAssembly);
                if (loadContext != null)
                {
                    Assembly assembly;
                    if (TryLoadAssembly(loadContext, assemblyName, out assembly))
                    {
                        return assembly;
                    }
                }
            }
            else
            {
                // Nothing worked, use the default load context
                return Default.LoadAssemblyImpl(assemblyName);
            }

            return null;
        }

        private static bool TryLoadAssembly(LoadContext context, AssemblyName assemblyName, out Assembly assembly)
        {
            assembly = context.LoadAssemblyImpl(assemblyName);

            if (assembly != null)
            {
                LoadContextAccessor.Instance.SetLoadContext(assembly, context);

                return true;
            }

            return false;
        }
    }
#endif
}
