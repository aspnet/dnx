using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
#if K10
using System.Runtime.Hosting.Loader;
using klr.core45.managed;
#else
using klr.net45.managed;
#endif
using System.Threading.Tasks;

namespace klr.hosting
{
    public static class RuntimeBootstrapper
    {
        private static Dictionary<string, Assembly> _assemblyCache = new Dictionary<string, Assembly>();
        private static readonly Dictionary<string, CommandOptionType> _options = new Dictionary<string, CommandOptionType>
        {
            { "lib", CommandOptionType.MultipleValue },
            { "verbosity", CommandOptionType.NoValue }
        };

        public static async Task<int> Execute(string[] args)
        {
            if (args.Length == 0)
            {
                return 1;
            }

#if NET45
            // TODO: Make this pluggable and not limited to the console logger
            var listener = new ConsoleTraceListener();
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
#endif
            var parser = new CommandLineParser();
            CommandOptions options;
            parser.ParseOptions(args, _options, out options);

            var libs = options.GetValues("lib");

            IList<string> searchPaths = libs == null ? new string[0] :
                libs.SelectMany(lib => lib.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFullPath)).ToArray();

            Func<string, Assembly> loader = _ => null;
            Func<byte[], Assembly> loadBytes = _ => null;
            Func<string, Assembly> loadFile = _ => null;

            Func<AssemblyName, Assembly> loaderCallback = assemblyName =>
            {
                try
                {
                    string name = assemblyName.Name;

                    // If the assembly was already loaded use it
                    Assembly assembly;
                    if (_assemblyCache.TryGetValue(name, out assembly))
                    {
                        return assembly;
                    }

                    assembly = loader(name) ?? ResolveHostAssembly(loadFile, searchPaths, name);

                    if (assembly != null)
                    {
                        ExtractAssemblyNeutralInterfaces(assembly, loadBytes);

                        _assemblyCache[name] = assembly;
                    }

                    return assembly;
                }
                catch (Exception ex)
                {
                    // Trace load failures
#if NET45
                    Trace.TraceError(String.Join(Environment.NewLine, GetExceptions(ex)));
#else
                    Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
#endif
                    throw;
                }
            };
#if K10
            var loaderImpl = new DelegateAssemblyLoadContext(loaderCallback);
            loadBytes = bytes => loaderImpl.LoadBytes(bytes, null);
            loadFile = path => loaderImpl.LoadFile(path);

            AssemblyLoadContext.Default = loaderImpl;
#else
            var loaderImpl = new LoaderEngine();
            loadBytes = bytes => loaderImpl.LoadBytes(bytes, null);
            loadFile = path => loaderImpl.LoadFile(path);

            ResolveEventHandler handler = (sender, a) =>
            {
                return loaderCallback(new AssemblyName(a.Name));
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;      
#endif

            try
            {
                Assembly.Load(new AssemblyName("Microsoft.Net.Runtime.Common, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));

                var assembly = Assembly.Load(new AssemblyName("klr.host"));


                // Loader impl
                // var loaderEngine = new DefaultLoaderEngine(loaderImpl);
                var loaderEngineType = assembly.GetType("klr.host.DefaultLoaderEngine");
                var loaderEngine = Activator.CreateInstance(loaderEngineType, loaderImpl);

                // The following code is doing:
                // var hostContainer = new klr.host.HostContainer();
                // var rootHost = new klr.host.RootHost(loaderEngine, searchPaths);
                // hostContainer.AddHost(rootHost);
                // var bootstrapper = new klr.host.Bootstrapper();
                // bootstrapper.Main(bootstrapperArgs);

                var hostContainerType = assembly.GetType("klr.host.HostContainer");
                var rootHostType = assembly.GetType("klr.host.RootHost");
                var hostContainer = Activator.CreateInstance(hostContainerType);
                var rootHost = Activator.CreateInstance(rootHostType, new object[] { loaderEngine, searchPaths });
                MethodInfo addHostMethodInfo = hostContainerType.GetTypeInfo().GetDeclaredMethod("AddHost");
                var disposable = (IDisposable)addHostMethodInfo.Invoke(hostContainer, new[] { rootHost });
                var hostContainerLoad = hostContainerType.GetTypeInfo().GetDeclaredMethod("Load");

#if NET45
                loader = (Func<string, Assembly>)Delegate.CreateDelegate(typeof(Func<string, Assembly>), hostContainer, hostContainerLoad);
#else
                // TODO: Remove this when we get delegate create delegate in the profile
                loader = name => hostContainerLoad.Invoke(hostContainer, new[] { name }) as Assembly;
#endif
                var bootstrapperType = assembly.GetType("klr.host.Bootstrapper");
                var mainMethod = bootstrapperType.GetTypeInfo().GetDeclaredMethod("Main");
                var bootstrapper = Activator.CreateInstance(bootstrapperType, hostContainer, loaderEngine);

                using (disposable)
                {
                    var bootstrapperArgs = new object[] 
                    {
                        options.RemainingArgs.ToArray()
                    };

                    return await (Task<int>)mainMethod.Invoke(bootstrapper, bootstrapperArgs);
                }
            }
            catch (Exception ex)
            {
#if NET45
                Trace.TraceError(String.Join(Environment.NewLine, GetExceptions(ex)));
#else
                Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
#endif
                return 1;
            }
            finally
            {
#if NET45
                AppDomain.CurrentDomain.AssemblyResolve -= handler;
#endif
            }
        }

        private static void ExtractAssemblyNeutralInterfaces(Assembly assembly, Func<byte[], Assembly> loadBytes)
        {
            // Embedded assemblies end with .dll
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(".dll"))
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(name);

                    if (_assemblyCache.ContainsKey(assemblyName))
                    {
                        continue;
                    }

                    // We're creating 2 streams under the covers on core clr
                    var ms = new MemoryStream();
                    assembly.GetManifestResourceStream(name).CopyTo(ms);
                    _assemblyCache[assemblyName] = loadBytes(ms.ToArray());
                }
            }
        }

        private static Assembly ResolveHostAssembly(Func<string, Assembly> loadFile, IList<string> searchPaths, string name)
        {
            foreach (var searchPath in searchPaths)
            {
                var path = Path.Combine(searchPath, name + ".dll");

                if (File.Exists(path))
                {
                    return loadFile(path);
                }
            }

            return null;
        }

        private static IEnumerable<string> GetExceptions(Exception ex)
        {
            while (ex != null)
            {
                if ((ex is TargetInvocationException))
                {
                    ex = ex.InnerException;
                    continue;
                }
                else if (ex.GetType().Name == "FileLoadException")
                {
                    if (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                        continue;
                    }
                }

                yield return ex.ToString();
                ex = ex.InnerException;
            }
        }
    }
}
