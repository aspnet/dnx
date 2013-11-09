using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Loader;

namespace K
{
    class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            string path = args.FirstOrDefault();

            if (String.IsNullOrEmpty(path))
            {
                Console.WriteLine("K.exe [path]");
                return;
            }

            if (Environment.GetEnvironmentVariable("HOST_TRACE_LEVEL") != "1")
            {
                var listener = new ConsoleTraceListener();
                listener.Filter = new ErrorFilter();
                Trace.Listeners.Add(listener);
                Trace.AutoFlush = true;
            }

            string exePath = Assembly.GetExecutingAssembly().Location;
            Environment.SetEnvironmentVariable("HOST_PROCESS", exePath);
            Environment.SetEnvironmentVariable("HOST_TRACE_LEVEL", "1");


            var host = new DefaultHost(path);
            host.Execute(name => ExecuteMain(path, name, args.Skip(1).ToArray()));
        }

        private static void ExecuteMain(string path, string name, string[] args)
        {
            var assembly = Assembly.Load(name);

            var program = assembly.GetType("Program") ?? assembly.GetTypes().FirstOrDefault(t => t.Name == "Program");

            if (program == null)
            {
                Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                return;
            }

            var main = program.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (main == null)
            {
                Console.WriteLine("'{0}' does not contain a static 'Main' method suitable for an entry point", name);
                return;
            }

            var parameters = main.GetParameters();
            if (parameters.Length == 0)
            {
                main.Invoke(null, null);
            }
            else if (parameters.Length == 1)
            {
                main.Invoke(null, new object[] { args });
            }
        }

        private class ErrorFilter : TraceFilter
        {
            public override bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id, string formatOrMessage, object[] args, object data1, object[] data)
            {
                return eventType == TraceEventType.Error;
            }
        }
    }
}
