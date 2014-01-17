
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;
using klr.host;

public class Bootstrapper
{
    private HostContainer _container;

    public int Main(int argc, string[] argv)
    {
        if (argc < 1)
        {
            Console.WriteLine("{app} [args]");
            return -1;
        }

        int exitCode = 0;

#if DESKTOP // CORECLR_TODO: Classic tracing
        var listener = new ConsoleTraceListener();
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;
#endif

        string path = Path.GetFullPath(argv[0]);

        _container = new HostContainer();

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        var host = new RootHost(path);

        using (_container.AddHost(host))
        {
            exitCode = ExecuteMain(path, argv.Skip(1).ToArray());
        }

        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;

        return exitCode;
    }

    private int ExecuteMain(string path, string[] args)
    {
        var assembly = _container.GetEntryPoint();

        if (assembly == null)
        {
            return -1;
        }

        string name = assembly.GetName().Name;

        var programType = assembly.GetType("Program") ?? assembly.DefinedTypes.Where(t => t.Name == "Program").Select(t => t.AsType()).FirstOrDefault();

        if (programType == null)
        {
            Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
            return -1;
        }

        // Invoke the constructor with the most arguments
        var ctor = programType.GetTypeInfo()
                              .DeclaredConstructors
                              .OrderByDescending(p => p.GetParameters().Length)
                              .FirstOrDefault();

        var parameterValues = ctor.GetParameters()
                                  .Select(Satisfy)
                                  .ToArray();

        object programInstance = ctor.Invoke(parameterValues);

        var main = programType.GetTypeInfo().GetDeclaredMethods("Main").FirstOrDefault();

        if (main == null)
        {
            Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
            return -1;
        }

        var parameters = main.GetParameters();
        object result = null;

        if (parameters.Length == 0)
        {
            result = main.Invoke(programInstance, null);
        }
        else if (parameters.Length == 1)
        {
            result = main.Invoke(programInstance, new object[] { args });
        }

        if (result is int)
        {
            return (int)result;
        }

        return 0;
    }

    private object Satisfy(ParameterInfo arg)
    {
        if (arg.ParameterType == typeof(IHostContainer))
        {
            return _container;
        }

        return null;
    }

    private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        // REVIEW: Do we need to support resources?
        if (args.Name.Contains(".resources"))
        {
            return null;
        }

        var name = new AssemblyName(args.Name).Name;

        return _container.Load(name);
    }
}
