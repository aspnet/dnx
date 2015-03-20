// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.PackageManager.Algorithms;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Caching;
using NuGet;

namespace Microsoft.Framework.PackageManager.List
{
    internal class DependencyListOperation
    {
        private const string Configuration = "Debug";

        private readonly FrameworkName _framework;
        private readonly DependencyListOptions _options;
        private readonly ApplicationHostContext _hostContext;

        public DependencyListOperation(DependencyListOptions options, FrameworkName framework)
        {
            _options = options;
            _framework = framework;
            _hostContext = CreateApplicationHostContext();
        }

        public bool Execute()
        {
            var libDictionary = _hostContext.DependencyWalker.Libraries.ToDictionary(desc => desc.Identity);

            // 1. Walk the graph of library dependencies
            var librariesTreeBuilder = new LibraryDependencyFinder(libDictionary);
            var root = librariesTreeBuilder.Build(new Library
            {
                Name = _options.Project.Name,
                Version = _options.Project.Version
            });

            if (!_options.ShowAssemblies)
            {
                Render(root);
                return true;
            }

            // 2. Walk the local dependencies
            var assemblyWalker = new AssemblyWalker(_framework, _hostContext, libDictionary, _options.RuntimeFolder);
            var assemblies = assemblyWalker.Walk(root);

            foreach (var assemblyName in assemblies.OrderBy(assemblyName => assemblyName))
            {
                _options.Reports.Information.WriteLine(assemblyName);
            }

            return true;
        }

        private void Render(IGraphNode<Library> root)
        {
            var renderer = new LibraryDependencyFlatRenderer(_options.HideDependents, _options.ResultsFilter, _options.Project.Dependencies.Select(dep => dep.LibraryRange.Name));
            var content = renderer.GetRenderContent(root);

            if (content.Any())
            {
                _options.Reports.Information.WriteLine("\n[Target framework {0} ({1})]\n",
                    _framework.ToString(), VersionUtility.GetShortFrameworkName(_framework));

                foreach (var line in content)
                {
                    _options.Reports.Information.WriteLine(line);
                }
            }
        }

        private ApplicationHostContext CreateApplicationHostContext()
        {
            var accessor = new CacheContextAccessor();
            var cache = new Cache(accessor);

            var hostContext = new ApplicationHostContext(
                serviceProvider: null,
                projectDirectory: _options.Project.ProjectDirectory,
                packagesDirectory: null,
                configuration: Configuration,
                targetFramework: _framework,
                cache: cache,
                cacheContextAccessor: accessor,
                namedCacheDependencyProvider: null);

            hostContext.DependencyWalker.Walk(hostContext.Project.Name, hostContext.Project.Version, _framework);

            return hostContext;
        }
    }
}