using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

public class EntryPoint
{
    public static void Main(string[] arguments)
    {
        // AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        // Assembly.Load("Microsoft.Net.Runtime.Interfaces");
        
        var assembly = Assembly.Load("klr.host");

        // AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomainOnAssemblyResolve;

        var bootstrapperType = assembly.GetType("Bootstrapper");
        var mainMethod = bootstrapperType.GetMethod("Main");
        var bootstrapper = Activator.CreateInstance(bootstrapperType);
        mainMethod.Invoke(bootstrapper, new object[] { arguments.Length, arguments });
    }

    private static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var name = new AssemblyName(args.Name).Name;
        var path = Path.Combine(dir, name + ".dll");
        Assembly assembly = Assembly.LoadFile(path);
        return assembly;
    }
}