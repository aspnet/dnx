// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.Infrastructure;
using Microsoft.Framework.Runtime.Loader;

namespace klr.host
{
    public class Bootstrapper
    {
        private readonly IAssemblyLoaderContainer _container;
        
        public Bootstrapper(IAssemblyLoaderContainer container)
        {
            _container = container;
        }

        public Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{app} [arguments]");
                return Task.FromResult(-1);
            }

            var name = args[0];
            var programArgs = args.Skip(1).ToArray();

            var assembly = Assembly.Load(new AssemblyName(name));

            if (assembly == null)
            {
                return Task.FromResult(-1);
            }

#if ASPNET50
            string applicationBaseDirectory;
            if (PlatformHelper.IsMono)
            {
                applicationBaseDirectory = Environment.GetEnvironmentVariable("KRE_APPBASE");
                if (string.IsNullOrEmpty(applicationBaseDirectory))
                {
                    applicationBaseDirectory = Directory.GetCurrentDirectory();
                }
            }
            else
            {
                applicationBaseDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            }
#else
            string applicationBaseDirectory = AppContext.BaseDirectory;
#endif

            var framework = Environment.GetEnvironmentVariable("TARGET_FRAMEWORK") ?? Environment.GetEnvironmentVariable("KRE_FRAMEWORK");
            var configuration = Environment.GetEnvironmentVariable("TARGET_CONFIGURATION") ?? Environment.GetEnvironmentVariable("KRE_CONFIGURATION") ?? "Debug";

            // TODO: Support the highest installed version
            var targetFramework = FrameworkNameUtility.ParseFrameworkName(framework ?? "aspnet50");

            var applicationEnvironment = new ApplicationEnvironment(applicationBaseDirectory,
                                                                    targetFramework,
                                                                    configuration,
                                                                    assembly: assembly);

            CallContextServiceLocator.Locator = new ServiceProviderLocator();

            var serviceProvider = new ServiceProvider();
            serviceProvider.Add(typeof(IAssemblyLoaderContainer), _container);
            serviceProvider.Add(typeof(IAssemblyLoadContextAccessor), LoadContextAccessor.Instance);
            serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);

            CallContextServiceLocator.Locator.ServiceProvider = serviceProvider;

            return EntryPointExecutor.Execute(assembly, programArgs, serviceProvider);
        }
    }
}
