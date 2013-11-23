
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
        if (argc < 2)
        {
            Console.WriteLine("[path] [args]");
            return -1;
        }

        var listener = new ConsoleTraceListener();
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        string path = Path.GetFullPath(argv[1]);

        _container = new HostContainer();

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        var host = new RootHost(path);

        using (_container.AddHost(host))
        {
            ExecuteMain(path, argv.Skip(2).ToArray());
        }

        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;

        return 0;
    }

    private void ExecuteMain(string path, string[] args)
    {
        var assembly = _container.GetEntryPoint();

        if (assembly == null)
        {
            return;
        }

        string name = assembly.GetName().Name;

        var programType = assembly.GetType("Program") ?? assembly.GetTypes().FirstOrDefault(t => t.Name == "Program");

        if (programType == null)
        {
            Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
            return;
        }

        // Invoke the constructor with the most arguments
        var ctor = programType.GetConstructors()
                              .OrderByDescending(p => p.GetParameters().Length)
                              .FirstOrDefault();

        var parameterValues = ctor.GetParameters()
                                  .Select(Satisfy)
                                  .ToArray();

        object programInstance = ctor.Invoke(parameterValues);

        var main = programType.GetMethod("Main", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (main == null)
        {
            Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
            return;
        }

        var parameters = main.GetParameters();
        if (parameters.Length == 0)
        {
            main.Invoke(programInstance, null);
        }
        else if (parameters.Length == 1)
        {
            main.Invoke(programInstance, new object[] { args });
        }
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
        return _container.Load(new AssemblyName(args.Name).Name);
    }
}
