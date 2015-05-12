// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Dependencies;
using Microsoft.Framework.Runtime.Internal;
using Microsoft.Framework.Runtime.Loader;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime
{
    public class RuntimeHostBuilder
    {
        public IList<IDependencyProvider> DependencyProviders { get; } = new List<IDependencyProvider>();
        public IList<IAssemblyLoaderFactory> Loaders { get; } = new List<IAssemblyLoaderFactory>();
        public NuGetFramework TargetFramework { get; set; }
        public Project Project { get; set; }
        public LockFile LockFile { get; set;  }
        public GlobalSettings GlobalSettings { get; set; }
        public IServiceProvider Services { get; set; }
        public PackagePathResolver PackagePathResolver { get; set; }

        /// <summary>
        /// Create a <see cref="RuntimeHostBuilder"/> for the project in the specified
        /// <paramref name="projectDirectory"/>, using the default configuration.
        /// </summary>
        /// <remarks>
        /// This method will throw if the project.json file cannot be found in the
        /// specified folder. If a project.lock.json file is present in the directory
        /// it will be loaded.
        /// </remarks>
        /// <param name="projectDirectory">The directory of the project to host</param>
        public static RuntimeHostBuilder ForProjectDirectory(string projectDirectory, NuGetFramework runtimeFramework, IServiceProvider services)
        {
            if (string.IsNullOrEmpty(projectDirectory))
            {
                throw new ArgumentNullException(nameof(projectDirectory));
            }
            if (runtimeFramework == null)
            {
                throw new ArgumentNullException(nameof(runtimeFramework));
            }

            var log = RuntimeLogging.Logger<RuntimeHostBuilder>();
            var hostBuilder = new RuntimeHostBuilder();

            // Load the Project
            var projectResolver = new PackageSpecResolver(projectDirectory);
            PackageSpec packageSpec;
            if (projectResolver.TryResolvePackageSpec(GetProjectName(projectDirectory), out packageSpec))
            {
                log.LogVerbose($"Loaded project {packageSpec.Name}");
                hostBuilder.Project = new Project(packageSpec);
            }
            hostBuilder.GlobalSettings = projectResolver.GlobalSettings;

            // Load the Lock File if present
            LockFile lockFile;
            if (TryReadLockFile(projectDirectory, out lockFile))
            {
                log.LogVerbose($"Loaded lock file");
                hostBuilder.LockFile = lockFile;
            }

            // Set the framework and other components
            hostBuilder.TargetFramework = runtimeFramework;
            hostBuilder.Services = services;
            hostBuilder.PackagePathResolver = new PackagePathResolver(
                ResolveRepositoryPath(hostBuilder.GlobalSettings), 
                GetCachePaths());

            log.LogVerbose("Registering PackageSpecReferenceDependencyProvider");
            hostBuilder.DependencyProviders.Add(new PackageSpecReferenceDependencyProvider(projectResolver));

            if (hostBuilder.LockFile != null)
            {
                log.LogVerbose("Registering LockFileDependencyProvider");
                hostBuilder.DependencyProviders.Add(new LockFileDependencyProvider(hostBuilder.LockFile));
            }

            log.LogVerbose("Registering ReferenceAssemblyDependencyProvider");
            var referenceResolver = new FrameworkReferenceResolver();
            hostBuilder.DependencyProviders.Add(new ReferenceAssemblyDependencyProvider(referenceResolver));

            // GAC resolver goes here! :)

            return hostBuilder;
        }

        /// <summary>
        /// Builds a <see cref="RuntimeHost"/> from the parameters specified in this
        /// object.
        /// </summary>
        public RuntimeHost Build()
        {
            return new RuntimeHost(this);
        }

        private static string ResolveRepositoryPath(GlobalSettings globalSettings)
        {
            // Order
            // 1. EnvironmentNames.Packages environment variable
            // 2. global.json { "packages": "..." }
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.Packages);

            if(string.IsNullOrEmpty(runtimePackages))
            {
                runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.DnxPackages);
            }

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

        private static IEnumerable<string> GetCachePaths()
        {
            var packageCachePathValue = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesCache);

            if (string.IsNullOrEmpty(packageCachePathValue))
            {
                return Enumerable.Empty<string>();
            }

            return packageCachePathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string GetProjectName(string projectDirectory)
        {
            projectDirectory = projectDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return projectDirectory.Substring(Path.GetDirectoryName(projectDirectory).Length).Trim(Path.DirectorySeparatorChar);
        }

        private static bool TryReadLockFile(string directory, out LockFile lockFile)
        {
            lockFile = null;
            string file = Path.Combine(directory, LockFileReader.LockFileName);
            if (File.Exists(file))
            {
                using (var stream = File.OpenRead(file))
                {
                    lockFile = LockFileReader.Read(stream);
                }
                return true;
            }
            return false;
        }

    }
}
