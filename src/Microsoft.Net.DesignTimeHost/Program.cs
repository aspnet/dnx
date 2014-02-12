using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime.Services;

namespace Microsoft.Net.DesignTimeHost
{
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;

        public Program(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Main(string[] args)
        {
            var watcher = (IFileMonitor)_serviceProvider.GetService(typeof(IFileMonitor));
            var mdProvider = (IProjectMetadataProvider)_serviceProvider.GetService(typeof(IProjectMetadataProvider));
            var info = (IApplicationEnvironment)_serviceProvider.GetService(typeof(IApplicationEnvironment));
            var refresher = (IDependencyRefresher)_serviceProvider.GetService(typeof(IDependencyRefresher));

            watcher.OnChanged += path =>
            {
                Console.WriteLine("Change notification " + path);

                if (Path.GetFileName(path).Equals("project.json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Console.WriteLine("Refreshing dependencies");
                        refresher.RefreshDependencies(info.ApplicationName, info.Version, info.TargetFramework);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        return;
                    }
                }

                PrintProjectData(info, mdProvider);
            };

            PrintProjectData(info, mdProvider);
            Console.ReadLine();
        }

        private static void PrintProjectData(IApplicationEnvironment info, IProjectMetadataProvider projectProvider)
        {
            try
            {
                var p = projectProvider.GetProjectMetadata(info.ApplicationName, new FrameworkName(".NETFramework", new Version(4, 5)));

                Console.WriteLine("Sources");
                foreach (var s in p.SourceFiles)
                {
                    Console.WriteLine(s);
                }

                Console.WriteLine("References");
                foreach (var r in p.References)
                {
                    Console.WriteLine(r);
                }

                Console.WriteLine("{0} RawReferences", p.RawReferences.Count);

                Console.WriteLine("Errors");
                foreach (var e in p.Errors)
                {
                    Console.WriteLine(e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
