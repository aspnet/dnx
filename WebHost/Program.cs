using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Starter;

namespace WebHost
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("webhost.exe [path] [url]");

                Environment.Exit(-1);
                return;
            }

            string path = Path.GetFullPath(args[0]);
            string url = args[1];

            var host = new DomainHostingStarter2();
            var options = new StartOptions();
            options.Settings["directory"] = path;
            options.Urls.Add(url);

            host.Start(options);

            Console.ReadLine();
        }
    }
}
