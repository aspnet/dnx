using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;

[SecurityCritical]
sealed class DomainManager : AppDomainManager
{
    private static Dictionary<string, Assembly> _hostAssemblies = new Dictionary<string, Assembly>();

    public DomainManager()
    {
    }

    public override bool CheckSecuritySettings(SecurityState state)
    {
        return true;
    }

    public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
    {
        base.InitializeNewDomain(appDomainInfo);
    }

#if NET45
    [HandleProcessCorruptedStateExceptions]
#endif
    [SecurityCritical]
    unsafe static int Main(int argc, char** argv)
    {
        if (argc == 0)
        {
            return 1;
        }

        // Pack arguments
        var arguments = new string[argc];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = new string(argv[i]);
        }

        // First argument is the path(s) to search for dependencies
        string rawSearchPaths = arguments[0];
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
            Assembly.Load(new AssemblyName("Stubs, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));
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
                return (int)mainMethod.Invoke(bootstrapper, new object[] { arguments.Skip(1).ToArray() });
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
}