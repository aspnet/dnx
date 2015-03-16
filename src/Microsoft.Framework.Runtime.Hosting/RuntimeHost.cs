// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Common;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.Dependencies;
using Microsoft.Framework.Runtime.Internal;
using Microsoft.Framework.Runtime.Loader;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime
{
    public class RuntimeHost
    {
        private readonly ILogger Log;

        public Project Project { get; }
        public GlobalSettings GlobalSettings { get; }
        public NuGetFramework TargetFramework { get; }
        public IEnumerable<IDependencyProvider> DependencyProviders { get; }
        public ILoggerFactory LoggerFactory { get; }
        public IServiceProvider Services { get; }

        internal RuntimeHost(RuntimeHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (builder.Project == null)
            {
                throw new ArgumentException($"{nameof(RuntimeHostBuilder)} does not contain a valid Project", nameof(builder));
            }

            Log = RuntimeLogging.Logger<RuntimeHost>();

            Project = builder.Project;
            GlobalSettings = builder.GlobalSettings;

            Services = builder.Services;

            // Load properties from the mutable RuntimeHostBuilder into
            // immutable copies on this object
            TargetFramework = builder.TargetFramework;

            // Copy the dependency providers so the user can't fiddle with them without our knowledge
            var list = new List<IDependencyProvider>(builder.DependencyProviders);
            DependencyProviders = list;
        }

        public Task<int> ExecuteApplication(
            IAssemblyLoaderContainer loaderContainer,
            IAssemblyLoadContextAccessor loadContextAccessor,
            string applicationName,
            string[] programArgs)
        {
            Log.LogInformation($"Launching '{applicationName}' '{string.Join(" ", programArgs)}'");

            var deps = DependencyManager.ResolveDependencies(
                    DependencyProviders,
                    Project.Name,
                    Project.Version,
                    TargetFramework);

            using (var loaderCleanup = new DisposableList())
            {
                // Set up assembly loaders
                loaderCleanup.Add(
                    loaderContainer.AddLoader(
                        new PackageAssemblyLoader(
                            deps.GetLibraries(LibraryTypes.Package),
                            TargetFramework,
                            new DefaultPackagePathResolver(ResolveRepositoryPath(GlobalSettings)),
                            loadContextAccessor)));

                // Locate the entry point
                var entryPoint = LocateEntryPoint(applicationName);

                if (Log.IsEnabled(LogLevel.Information))
                {
                    Log.LogInformation($"Executing Entry Point: {entryPoint.GetName().FullName}");
                }
                return EntryPointExecutor.Execute(entryPoint, programArgs, Services);
            }
        }

        private Assembly LocateEntryPoint(string applicationName)
        {
            using (Log.LogTimedMethod())
            {
                Log.LogInformation($"Locating entry point for {applicationName}");

                if (Project == null)
                {
                    Log.LogError("Unable to locate entry point, there is no project");
                    return null;
                }

                Assembly asm = null;
                try
                {
                    asm = Assembly.Load(new AssemblyName(applicationName));
                }
                catch (FileLoadException ex) when (string.Equals(new AssemblyName(ex.FileName).Name, applicationName, StringComparison.Ordinal))
                {
                    if (ex.InnerException is ICompilationException)
                    {
                        throw ex.InnerException;
                    }

                    ThrowEntryPointNotFoundException(applicationName, ex);
                }
                catch (FileNotFoundException ex) when (string.Equals(ex.FileName, applicationName, StringComparison.Ordinal))
                {
                    if (ex.InnerException is ICompilationException)
                    {
                        throw ex.InnerException;
                    }

                    ThrowEntryPointNotFoundException(applicationName, ex);
                }

                return asm;
            }
        }

        private void ThrowEntryPointNotFoundException(
            string applicationName,
            Exception innerException)
        {
            if (Project.Commands.Any())
            {
                // Throw a nicer exception message if the command
                // can't be found
                throw new InvalidOperationException(
                    string.Format("Unable to load application or execute command '{0}'. Available commands: {1}.",
                    applicationName,
                    string.Join(", ", Project.Commands.Keys)), innerException);
            }

            throw new InvalidOperationException(
                    string.Format("Unable to load application or execute command '{0}'.",
                    applicationName), innerException);
        }

        private static string ResolveRepositoryPath(GlobalSettings globalSettings)
        {
            // Order
            // 1. EnvironmentNames.Packages environment variable
            // 2. global.json { "packages": "..." }
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.Packages);

            if (!string.IsNullOrEmpty(runtimePackages))
            {
                return runtimePackages;
            }

            if (!string.IsNullOrEmpty(globalSettings?.PackagesPath))
            {
                return Path.Combine(globalSettings.RootPath, globalSettings.PackagesPath);
            }

            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            return Path.Combine(profileDirectory, Constants.DefaultLocalRuntimeHomeDir, "packages");
        }

        private class DisposableList : List<IDisposable>, IDisposable
        {
            public void Dispose()
            {
                foreach (var item in this)
                {
                    item.Dispose();
                }
            }
        }
    }
}
