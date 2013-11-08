using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Starter;

namespace OwinHost2
{
    class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            string path = args.FirstOrDefault();

            if (String.IsNullOrEmpty(path))
            {
                Console.WriteLine("owinhost2.exe [path]");

                Environment.Exit(-1);
                return;
            }

            var host = new DomainHostingStarter2();
            var options = new StartOptions();
            options.Settings["directory"] = path;
            options.Urls.Add("http://localhost:8081");

            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;

            host.Start(options);

            Console.ReadLine();
        }
    }
}
