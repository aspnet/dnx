using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

public class DomainManager : AppDomainManager
{
    private ApplicationMainInfo _info;
    private string _originalApplicationBase;

    private static Dictionary<string, Assembly> _hostAssemblies = new Dictionary<string, Assembly>();

    public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
    {
        _info.Main = Main;
        BindApplicationMain(ref _info);

        if (!string.IsNullOrEmpty(_info.ApplicationBase))
        {
            _originalApplicationBase = appDomainInfo.ApplicationBase;
            appDomainInfo.ApplicationBase = _info.ApplicationBase;
        }
    }

    private int Main(int argc, string[] argv)
    {
        if (argc == 0)
        {
            return 1;
        }

        // TODO: Make this pluggable and not limited to the console logger
        var listener = new ConsoleTraceListener();
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        // First argument is the path(s) to search for dependencies
        string rawSearchPaths = argv[0];
        string[] searchPaths = rawSearchPaths.Split(';').Select(Path.GetFullPath).ToArray();

        Func<string, Assembly> loader = _ => null;

        ResolveEventHandler handler = (sender, args) =>
        {
            var name = new AssemblyName(args.Name).Name;

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

            loader = name => hostContainerLoad.Invoke(hostContainer, new[] { name }) as Assembly;

            var bootstrapperType = assembly.GetType("klr.host.Bootstrapper");
            var mainMethod = bootstrapperType.GetTypeInfo().GetDeclaredMethod("Main");
            var bootstrapper = Activator.CreateInstance(bootstrapperType, hostContainer);

            using (disposable)
            {
                return (int)mainMethod.Invoke(bootstrapper, new object[] { argv.Skip(1).ToArray() });
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
            return 1;
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
        }
    }

    private static Assembly ResolveHostAssembly(string[] searchPaths, string name)
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

    [DllImport("klr.net45.dll")]
    private extern static void BindApplicationMain(ref ApplicationMainInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct ApplicationMainInfo
    {
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public MainDelegate Main;

        [MarshalAs(UnmanagedType.BStr)]
        public String ApplicationBase;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int MainDelegate(
        int argc,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0)] String[] argv);
}
