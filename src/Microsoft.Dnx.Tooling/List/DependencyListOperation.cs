// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Tooling.Algorithms;
using NuGet;

namespace Microsoft.Dnx.Tooling.List
{
    internal class DependencyListOperation
    {
        private readonly FrameworkName _framework;
        private readonly DependencyListOptions _options;
        private readonly LibraryManager _libraryManager;

        public DependencyListOperation(DependencyListOptions options, FrameworkName framework)
        {
            _options = options;
            _framework = framework;
            _libraryManager = CreateLibraryManager();
        }

        public bool Execute()
        {
            // 1. Walk the graph of library dependencies
            var root = LibraryDependencyFinder.Build(_libraryManager.GetLibraryDescriptions(), _options.Project);

            if (_options.Mismatched)
            {
                RenderMismatchedDependencies(root);
                return true;
            }

            if (!_options.ShowAssemblies)
            {
                RenderAllDependencies(root);
                return true;
            }

            var assemblyPaths = PackageDependencyProvider.ResolvePackageAssemblyPaths(_libraryManager.GetLibraryDescriptions());

            // 2. Walk the local dependencies and print the assemblies list
            var assemblyWalker = new AssemblyWalker(_framework,
                                                    assemblyPaths,
                                                    _options.RuntimeFolder,
                                                    _options.Details,
                                                    _options.Reports);
            assemblyWalker.Walk(root);

            return true;
        }

        private void RenderMismatchedDependencies(IGraphNode<LibraryDependency> root)
        {
            var render = new MismatchedDependencyRenderer(_options, _framework);
            render.Render(root);
        }

        private void RenderAllDependencies(IGraphNode<LibraryDependency> root)
        {
            var renderer = new LibraryDependencyFlatRenderer(_options.Details,
                                                             _options.ResultsFilter,
                                                             _options.Project.Dependencies.Select(dep => dep.LibraryRange.Name));
            var content = renderer.GetRenderContent(root);

            if (content.Any())
            {
                _options.Reports.WriteInformation($"\n[Target framework {_framework} ({VersionUtility.GetShortFrameworkName(_framework)})]\n");

                foreach (var line in content)
                {
                    _options.Reports.Information.WriteLine(line);
                }
            }
        }

        private LibraryManager CreateLibraryManager()
        {
            var hostContext = new ApplicationHostContext
            {
                Project = _options.Project,
                TargetFramework = _framework
            };

            ApplicationHostContext.Initialize(hostContext);

            return hostContext.LibraryManager;
        }
    }
}
