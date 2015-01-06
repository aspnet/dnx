// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.Bundle
{
    public class DependencyContext
    {
        public DependencyContext(string projectDirectory, string configuration, FrameworkName targetFramework)
        {
            var cacheContextAccessor = new CacheContextAccessor();
            var cache = new Cache(cacheContextAccessor);

            var applicationHostContext = new ApplicationHostContext(
                serviceProvider: null,
                projectDirectory: projectDirectory,
                packagesDirectory: null,
                configuration: configuration,
                targetFramework: targetFramework,
                cache: cache,
                cacheContextAccessor: cacheContextAccessor,
                namedCacheDependencyProvider: new NamedCacheDependencyProvider());

            ProjectResolver = applicationHostContext.ProjectResolver;
            NuGetDependencyResolver = applicationHostContext.NuGetDependencyProvider;
            ProjectReferenceDependencyProvider = applicationHostContext.ProjectDepencyProvider;
            DependencyWalker = applicationHostContext.DependencyWalker;
            FrameworkName = targetFramework;
            PackagesDirectory = applicationHostContext.PackagesDirectory;
        }

        public IProjectResolver ProjectResolver { get; set; }
        public NuGetDependencyResolver NuGetDependencyResolver { get; set; }
        public ProjectReferenceDependencyProvider ProjectReferenceDependencyProvider { get; set; }
        public DependencyWalker DependencyWalker { get; set; }
        public FrameworkName FrameworkName { get; set; }
        public ILookup<string, PackageAssembly> PackageAssemblies { get; set; }
        public string PackagesDirectory { get; private set; }

        public void Walk(string projectName, SemanticVersion projectVersion)
        {
            DependencyWalker.Walk(projectName, projectVersion, FrameworkName);
            PackageAssemblies = NuGetDependencyResolver.PackageAssemblyLookup.Values.ToLookup(a => a.Library.Identity.Name);
        }

        public static FrameworkName GetFrameworkNameForRuntime(string runtime)
        {
            var parts = runtime.Split(new[] { '.' }, 2);
            if (parts.Length != 2)
            {
                return null;
            }
            parts = parts[0].Split(new[] { '-' }, 4);
            if (parts.Length < 2)
            {
                return null;
            }
            if (parts.Length == 2 && !string.Equals(parts[1].ToLowerInvariant(), "mono"))
            {
                return null;
            }
            switch (parts[1].ToLowerInvariant())
            {
                case "mono":
                case "clr":
                    return VersionUtility.ParseFrameworkName("aspnet50");
                case "coreclr":
                    return VersionUtility.ParseFrameworkName("aspnetcore50");
            }
            return null;
        }
    }
}