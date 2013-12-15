using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
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

#if DESKTOP
    [HandleProcessCorruptedStateExceptions]
#endif
    [SecurityCritical]
    unsafe static int Main(char* appBase, int argc, char** argv)
    {
        var applicationBase = new string(appBase);

        ResolveEventHandler handler = (sender, args) =>
        {
            var name = new AssemblyName(args.Name).Name;
            return ResolveAssembly(applicationBase, name);
        };

        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += handler;

            Assembly.Load(new AssemblyName("Stubs, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));

            Assembly.Load(new AssemblyName("Microsoft.Net.Runtime.Interfaces, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));

            var assembly = Assembly.Load(new AssemblyName("klr.host"));

            AppDomain.CurrentDomain.AssemblyResolve -= handler;

            //Pack arguments
            var arguments = new string[argc];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = new string(argv[i]);
            }

            var bootstrapperType = assembly.GetType("Bootstrapper");
            var mainMethod = bootstrapperType.GetTypeInfo().GetDeclaredMethod("Main");
            var bootstrapper = Activator.CreateInstance(bootstrapperType);
            var result = mainMethod.Invoke(bootstrapper, new object[] { argc, arguments });
            return (int)result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
            return 1;
        }
    }

    private static Assembly ResolveAssembly(string appBase, string name)
    {
        var searchPaths = new[] { 
            "", 
            Path.Combine(name, "bin", "k10"), 
            Path.Combine(name, "bin", "coreclr10") 
        };

        foreach (var searchPath in searchPaths)
        {
            var path = Path.Combine(appBase,
                                    searchPath,
                                    name + ".dll");

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
}