using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

public class DomainManager : AppDomainManager
{
    private ApplicationMainInfo _info;
    private string _originalApplicationBase;

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

        var resolver = new AssemblyResolver();

        IDisposable bootstrapperRegistration = resolver.RegisterLoader(assemblyName => ResolveAssembly(searchPaths, assemblyName.Name));

        Func<Func<AssemblyName, Assembly>, IDisposable> setAssemblyLoader = resolver.RegisterLoader;

        ResolveEventHandler handler = (sender, args) =>
        {
            return resolver.Load(new AssemblyName(args.Name));
        };

        AppDomain.CurrentDomain.AssemblyResolve += handler;

        try
        {
            // Eager load the dependencies of klr host then get out of the way
            Assembly.Load("Microsoft.Net.Runtime.Interfaces, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var assembly = Assembly.Load(new AssemblyName("klr.host"));
            bootstrapperRegistration.Dispose();

            var bootstrapperType = assembly.GetType("klr.host.Bootstrapper");
            var mainMethod = bootstrapperType.GetTypeInfo().GetDeclaredMethod("Main");
            var bootstrapper = Activator.CreateInstance(bootstrapperType, setAssemblyLoader);
            var result = mainMethod.Invoke(bootstrapper, new object[] { argv });
            return (int)result;
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

    private static Assembly ResolveAssembly(string[] searchPaths, string name)
    {
        foreach (var searchPath in searchPaths)
        {
            var path = Path.Combine(searchPath, name + ".dll");

            if (File.Exists(path))
            {
                return Assembly.LoadFile(path);
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

    private class AssemblyResolver
    {
        private List<Func<AssemblyName, Assembly>> _resolvers = new List<Func<AssemblyName, Assembly>>();

        public IDisposable RegisterLoader(Func<AssemblyName, Assembly> next)
        {
            _resolvers.Add(next);

            return new DisposableAction(() =>
            {
                _resolvers.Remove(next);
            });
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            foreach (var resolver in _resolvers)
            {
                Assembly assembly = resolver(assemblyName);

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
