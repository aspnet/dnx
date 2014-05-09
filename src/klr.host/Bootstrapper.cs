
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.Infrastructure;

namespace klr.host
{
    public class Bootstrapper
    {
        private readonly IHostContainer _container;
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public Bootstrapper(IHostContainer container,
                            IAssemblyLoaderEngine loaderEngine)
        {
            _container = container;
            _loaderEngine = loaderEngine;
        }

        public Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{app} [args]");
                return Task.FromResult(-1);
            }

            var name = args[0];
            var programArgs = args.Skip(1).ToArray();

            var assembly = Assembly.Load(new AssemblyName(name));

            if (assembly == null)
            {
                return Task.FromResult(-1);
            }

#if NET45
            // REVIEW: Need a way to set the application base on mono
            string applicationBaseDirectory = PlatformHelper.IsMono ? 
                                              Directory.GetCurrentDirectory() : 
                                              AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
#else
            string applicationBaseDirectory = ApplicationContext.BaseDirectory;
#endif

            var framework = Environment.GetEnvironmentVariable("TARGET_FRAMEWORK");
            var targetFramework = FrameworkNameUtility.ParseFrameworkName(framework ?? (PlatformHelper.IsMono ? "net45" : "net451"));

            var applicationEnvironment = new ApplicationEnvironment(applicationBaseDirectory,
                                                                    targetFramework,
                                                                    assembly);

            CallContextServiceLocator.Locator = new ServiceProviderLocator();

            var serviceProvider = new ServiceProvider();
            serviceProvider.Add(typeof(IHostContainer), _container);
            serviceProvider.Add(typeof(IAssemblyLoaderEngine), _loaderEngine);
            serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);

            CallContextServiceLocator.Locator.ServiceProvider = serviceProvider;

            return EntryPointExecutor.Execute(assembly, programArgs, serviceProvider);
        }
    }
}