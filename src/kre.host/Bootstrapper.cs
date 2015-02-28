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

namespace kre.host
{
    public class Bootstrapper
    {
        private readonly string[] _searchPaths;

        public Bootstrapper(string[] searchPaths)
        {
            _searchPaths = searchPaths;
        }

        public Task<int> RunAsync(string[] args)
        {
            var accessor = LoadContextAccessor.Instance;
            var container = new LoaderContainer();
            LoadContext.InitializeDefaultContext(new DefaultLoadContext(container));

            var disposable = container.AddLoader(new PathBasedAssemblyLoader(accessor, _searchPaths));

            try
            {
                var name = args[0];
                var programArgs = args.Skip(1).ToArray();

                var assembly = Assembly.Load(new AssemblyName(name));

                if (assembly == null)
                {
                    return Task.FromResult(-1);
                }

#if ASPNET50
                string applicationBaseDirectory = Environment.GetEnvironmentVariable(EnvironmentNames.AppBase);

                if (string.IsNullOrEmpty(applicationBaseDirectory))
                {
                    applicationBaseDirectory = Directory.GetCurrentDirectory();
                }
#else
                string applicationBaseDirectory = AppContext.BaseDirectory;
#endif

                var framework = Environment.GetEnvironmentVariable("TARGET_FRAMEWORK") ?? Environment.GetEnvironmentVariable(EnvironmentNames.Framework);
                var configuration = Environment.GetEnvironmentVariable("TARGET_CONFIGURATION") ?? Environment.GetEnvironmentVariable(EnvironmentNames.Configuration) ?? "Debug";

                // TODO: Support the highest installed version
                var targetFramework = FrameworkNameUtility.ParseFrameworkName(framework ?? "aspnet50");

                var applicationEnvironment = new ApplicationEnvironment(applicationBaseDirectory,
                                                                        targetFramework,
                                                                        configuration,
                                                                        assembly);

                CallContextServiceLocator.Locator = new ServiceProviderLocator();

                var serviceProvider = new ServiceProvider();
                serviceProvider.Add(typeof(IAssemblyLoaderContainer), container);
                serviceProvider.Add(typeof(IAssemblyLoadContextAccessor), accessor);
                serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);

                CallContextServiceLocator.Locator.ServiceProvider = serviceProvider;

                var task = EntryPointExecutor.Execute(assembly, programArgs, serviceProvider);

                return task.ContinueWith(async (t, state) =>
                {
                    // Dispose the host
                    ((IDisposable)state).Dispose();

                    return await t;
                }, disposable).Unwrap();
            }
            catch
            {
                disposable.Dispose();

                throw;
            }
        }
    }
}
