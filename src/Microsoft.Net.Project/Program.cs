using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;

namespace Microsoft.Net.Project
{
    public class Program
    {
        public void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("k [command] [application]");
                Environment.Exit(-1);
                return;
            }

            string command = args[0];
            string application = args.Length >= 2 ? args[1] : Directory.GetCurrentDirectory();

            try
            {
                if (command.Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    DefaultHost.Build(application);
                }
                else if (command.Equals("clean", StringComparison.OrdinalIgnoreCase))
                {
                    DefaultHost.Clean(application);
                }
                else
                {
                    Console.WriteLine("unknown command '{0}'", command);
                    Environment.Exit(-1);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
                Environment.Exit(-2);
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
                yield return ex.ToString();
            }
        }
    }
}
