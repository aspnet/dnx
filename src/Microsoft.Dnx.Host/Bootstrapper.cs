// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Common;
using Microsoft.Dnx.Runtime.Loader;

namespace Microsoft.Dnx.Host
{
    public class Bootstrapper
    {
        private readonly IEnumerable<string> _searchPaths;

        public Bootstrapper(IEnumerable<string> searchPaths)
        {
            _searchPaths = searchPaths;
        }

        public int Run(List<string> args, IRuntimeEnvironment env, string appBase, FrameworkName targetFramework)
        {
            var accessor = LoadContextAccessor.Instance;
            var container = new LoaderContainer();
            LoadContext.InitializeDefaultContext(new DefaultLoadContext(container));

            var disposable = container.AddLoader(new PathBasedAssemblyLoader(accessor, _searchPaths));

            var name = args[0];
            var programArgs = new string[args.Count - 1];
            args.CopyTo(1, programArgs, 0, programArgs.Length);

            var assembly = accessor.Default.Load(name);

            if (assembly == null)
            {
                return 1;
            }

#if DNX451
            string applicationBaseDirectory = appBase;

            // Set the app domain variable so that AppContext.BaseDirectory works on .NET Framework (and hopefully mono)
            AppDomain.CurrentDomain.SetData("APP_CONTEXT_BASE_DIRECTORY", applicationBaseDirectory);
#else
            var applicationBaseDirectory = AppContext.BaseDirectory;
#endif

            var configuration = Environment.GetEnvironmentVariable("TARGET_CONFIGURATION") ?? Environment.GetEnvironmentVariable(EnvironmentNames.Configuration) ?? "Debug";
            Logger.TraceInformation($"[{nameof(Bootstrapper)}] Runtime Framework: {targetFramework}");

            var applicationEnvironment = new HostApplicationEnvironment(applicationBaseDirectory,
                                                                        targetFramework,
                                                                        configuration,
                                                                        assembly);

            PlatformServices.SetDefault(new DnxHostPlatformServices(applicationEnvironment, env, container, accessor));

            using (disposable)
            {
                return EntryPointExecutor.ExecuteAssembly(assembly, programArgs);
            }
        }
    }
}
