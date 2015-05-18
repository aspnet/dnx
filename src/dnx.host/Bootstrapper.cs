// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.Common.Impl;
using Microsoft.Framework.Runtime.Infrastructure;
using Microsoft.Framework.Runtime.Loader;

namespace dnx.host
{
    public class Bootstrapper
    {
        private readonly IEnumerable<string> _searchPaths;

        public Bootstrapper(IEnumerable<string> searchPaths)
        {
            _searchPaths = searchPaths;
        }

        public Task<int> RunAsync(List<string> args, IRuntimeEnvironment env)
        {
            var accessor = LoadContextAccessor.Instance;
            var container = new LoaderContainer();
            LoadContext.InitializeDefaultContext(new DefaultLoadContext(container));

            var disposable = container.AddLoader(new PathBasedAssemblyLoader(accessor, _searchPaths));

            try
            {
                var name = args[0];
                var programArgs = new string[args.Count - 1];
                args.CopyTo(1, programArgs, 0, programArgs.Length);

                var assembly = accessor.Default.Load(name);

                if (assembly == null)
                {
                    return Task.FromResult(-1);
                }

#if DNX451
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

                var targetFramework = FrameworkNameUtility.ParseFrameworkName(framework ?? FrameworkNames.ShortNames.Dnx451);

                var applicationEnvironment = new ApplicationEnvironment(applicationBaseDirectory,
                                                                        targetFramework,
                                                                        configuration,
                                                                        assembly);

                CallContextServiceLocator.Locator = new ServiceProviderLocator();

                var serviceProvider = new ServiceProvider();
                serviceProvider.Add(typeof(IAssemblyLoaderContainer), container);
                serviceProvider.Add(typeof(IAssemblyLoadContextAccessor), accessor);
                serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);
                serviceProvider.Add(typeof(IRuntimeEnvironment), env);

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
