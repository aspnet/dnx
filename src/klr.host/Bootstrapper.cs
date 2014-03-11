
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Common;
using Microsoft.Net.Runtime.Common.DependencyInjection;

namespace klr.host
{
    public class Bootstrapper
    {
        private readonly IHostContainer _container;
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly IDependencyExporter _exporter;

        public Bootstrapper(IHostContainer container,
                            IAssemblyLoaderEngine loaderEngine,
                            IDependencyExporter exporter)
        {
            _container = container;
            _loaderEngine = loaderEngine;
            _exporter = exporter;
        }

        public async Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{app} [args]");
                return -1;
            }

            var name = args[0];
            var programArgs = args.Skip(1).ToArray();

            var assembly = Assembly.Load(new AssemblyName(name));

            if (assembly == null)
            {
                return -1;
            }

#if NET45
            string applicationBaseDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
#else
            string applicationBaseDirectory = (string)typeof(AppDomain).GetRuntimeMethod("GetData", new[] { typeof(string) }).Invoke(AppDomain.CurrentDomain, new object[] { "APPBASE" });
#endif

            var framework = Environment.GetEnvironmentVariable("TARGET_FRAMEWORK");
            var targetFramework = FrameworkNameUtility.ParseFrameworkName(framework ?? "net45");

            var applicationEnvironment = new ApplicationEnvironment(applicationBaseDirectory,
                                                                    targetFramework,
                                                                    assembly);

            var serviceProvider = new ServiceProvider();
            serviceProvider.Add(typeof(IHostContainer), _container);
            serviceProvider.Add(typeof(IAssemblyLoaderEngine), _loaderEngine);
            serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);
            serviceProvider.Add(typeof(IDependencyExporter), _exporter);

            return await EntryPointExecutor.Execute(assembly, programArgs, serviceProvider);
        }
    }
}