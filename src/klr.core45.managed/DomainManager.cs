using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;

[SecurityCritical]
sealed class DomainManager : AppDomainManager
{

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
            Assembly.Load(new AssemblyName("Stubs, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));
            Assembly.Load(new AssemblyName("Microsoft.Net.Runtime.Interfaces, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));
            var assembly = Assembly.Load(new AssemblyName("klr.host"));
            bootstrapperRegistration.Dispose();

            var bootstrapperType = assembly.GetType("klr.host.Bootstrapper");
            var mainMethod = bootstrapperType.GetTypeInfo().GetDeclaredMethod("Main");
            var bootstrapper = Activator.CreateInstance(bootstrapperType, setAssemblyLoader);
            var result = mainMethod.Invoke(bootstrapper, new object[] { arguments });
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
}