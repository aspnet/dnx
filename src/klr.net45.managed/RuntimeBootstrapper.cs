using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace klr.hosting
{
    public static class RuntimeBootstrapper
    {
        private static Dictionary<string, Assembly> _hostAssemblies = new Dictionary<string, Assembly>();
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
                libs.SelectMany(lib => lib.Split(';').Select(Path.GetFullPath)).ToArray();

            Func<string, Assembly> loader = _ => null;

            ResolveEventHandler handler = (sender, a) =>
            {
                var name = new AssemblyName(a.Name).Name;

                // If host assembly was already loaded use it
                Assembly assembly;
                if (_hostAssemblies.TryGetValue(name, out assembly))
                {
                    return assembly;
                }

                // Special case for host assemblies
                return loader(name) ?? ResolveHostAssembly(searchPaths, name);
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;

            try
            {
                Assembly.Load(new AssemblyName("Microsoft.Net.Runtime.Common, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));

                var assembly = Assembly.Load(new AssemblyName("klr.host"));

                // The following code is doing:
                // var hostContainer = new klr.host.HostContainer();
                // var rootHost = new klr.host.RootHost(searchPaths);
                // hostContainer.AddHost(rootHost);
                // var bootstrapper = new klr.host.Bootstrapper();
                // bootstrapper.Main(argv.Skip(1).ToArray());

                var hostContainerType = assembly.GetType("klr.host.HostContainer");
                var rootHostType = assembly.GetType("klr.host.RootHost");
                var hostContainer = Activator.CreateInstance(hostContainerType);
                var rootHost = Activator.CreateInstance(rootHostType, new object[] { searchPaths });
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
                var bootstrapper = Activator.CreateInstance(bootstrapperType, hostContainer);

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
                AppDomain.CurrentDomain.AssemblyResolve -= handler;
            }
        }

        private static Assembly ResolveHostAssembly(IList<string> searchPaths, string name)
        {
            foreach (var searchPath in searchPaths)
            {
                var path = Path.Combine(searchPath, name + ".dll");

                if (File.Exists(path))
                {
                    var assembly = Assembly.LoadFile(path);

                    _hostAssemblies[name] = assembly;
                    return assembly;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetExceptions(Exception ex)
        {
            if (ex.InnerException != null)
            {
                foreach (var e in GetExceptions(ex.InnerException))
                {
                    yield return e;
                }
            }

            if (!(ex is TargetInvocationException))
            {
                yield return ex.ToString();
            }
        }
    }
}
