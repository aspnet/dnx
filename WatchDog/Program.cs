using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WatchDog
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = args.FirstOrDefault();

            if (String.IsNullOrEmpty(path))
            {
                Console.WriteLine("watchdog.exe [path] [args]");
                Environment.Exit(-1);
                return;
            }

            string childProcess = Path.GetFullPath(path);
            string childArgs = String.Join(" ", args.Skip(1));

            while (true)
            {
                Console.Write("Starting process '" + Path.GetFileName(childProcess) + "'");
                if (!String.IsNullOrEmpty(childArgs))
                {
                    Console.WriteLine(" with " + childArgs);
                }
                else
                {
                    Console.WriteLine();
                }

                var exe = new Executable(childProcess, Environment.CurrentDirectory, TimeSpan.FromHours(1));
                var exitCode = exe.Execute(s =>
                {
                    Console.WriteLine(s);
                    return false;
                },
                s =>
                {
                    Console.Error.WriteLine(s);
                    return false;
                },
                Encoding.UTF8, childArgs);

                if (exitCode != 250)
                {
                    Console.WriteLine("Exit code unknown {0}, quitting", exitCode);
                    break;
                }

                Thread.Sleep(100);
            }
        }
    }
}
