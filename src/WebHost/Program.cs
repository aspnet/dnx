using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Owin.Hosting.Starter;

namespace WebHost
{
    public class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("webhost [path] [url]");

                Environment.Exit(-1);
                return;
            }

            string path = Path.GetFullPath(args[0]);
            string url = args[1];

            var host = new HostStarter();

            try
            {
                host.Start(path, url);

                Console.ReadLine();
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
                Environment.Exit(-1);
            }
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
                yield return ex.Message;
            }
        }
    }
}
