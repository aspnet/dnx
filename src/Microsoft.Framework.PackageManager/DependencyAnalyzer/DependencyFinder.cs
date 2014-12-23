// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.DependencyAnalyzer
{
    internal class DependencyFinder
    {
        private const string LibraryTypeProject = "Project";
        private const string Configuration = "Debug";

        private readonly ICache _cache;
        private readonly ICacheContextAccessor _accessor;
        private readonly string _kreSourceRoot;
        private readonly FrameworkName _framework;
        private readonly Func<ApplicationHostContext, IDependenyResolver> _createResolver;

        public DependencyFinder(
            string kreSourceRoot,
            FrameworkName framework,
            Func<ApplicationHostContext, IDependenyResolver> createResolver)
        {
            _accessor = new CacheContextAccessor();
            _cache = new Cache(_accessor);
            _kreSourceRoot = kreSourceRoot;
            _framework = framework;

            _createResolver = createResolver;
        }

        public HashSet<string> GetDependencies(string projectName)
        {
            var usedAssemblies = new HashSet<string>();
            var hostContext = GetApplicationHostContext(projectName);
            var nonProjectLibraries = GetNonProjectLibraries(hostContext);
            var resolver = _createResolver(hostContext);

            foreach (var library in nonProjectLibraries)
            {
                foreach (var loadableAssembly in library.LoadableAssemblies)
                {
                    usedAssemblies.AddRange(resolver.Resolve(loadableAssembly.Name));
                }
            }

            return usedAssemblies;
        }

        protected ApplicationHostContext GetApplicationHostContext(string projectName)
        {
            var projectFolder = Path.Combine(_kreSourceRoot, projectName);

            var hostContext = new ApplicationHostContext(serviceProvider: null,
                                                    projectDirectory: projectFolder,
                                                    packagesDirectory: null,
                                                    configuration: Configuration,
                                                    targetFramework: _framework,
                                                    cache: _cache,
                                                    cacheContextAccessor: _accessor,
                                                    namedCacheDependencyProvider: null);

            hostContext.DependencyWalker.Walk(hostContext.Project.Name, hostContext.Project.Version, _framework);

            return hostContext;
        }

        protected IEnumerable<ILibraryInformation> GetNonProjectLibraries(ApplicationHostContext hostContext)
        {
            return hostContext.LibraryManager
                              .GetLibraries()
                              .Where(library => !string.Equals(LibraryTypeProject, library.Type, StringComparison.OrdinalIgnoreCase));
        }
    }
}