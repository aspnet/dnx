// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Tooling.Algorithms;
using NuGet;

namespace Microsoft.Dnx.Tooling.List
{
    internal class DependencyListOperation
    {
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
            // 1. Walk the graph of library dependencies
            var root = LibraryDependencyFinder.Build(_hostContext.LibraryManager.GetLibraryDescriptions(), _options.Project);

            if (!_options.ShowAssemblies)
            {
                Render(root);
                return true;
            }

            // 2. Walk the local dependencies and print the assemblies list
            var assemblyWalker = new AssemblyWalker(_framework,
                                                    _hostContext,
                                                    _options.RuntimeFolder,
                                                    _options.Details,
                                                    _options.Reports);
            assemblyWalker.Walk(root);

            return true;
        }

        private void Render(IGraphNode<LibraryDescription> root)
        {
            var renderer = new LibraryDependencyFlatRenderer(_options.Details,
                                                             _options.ResultsFilter,
                                                             _options.Project.Dependencies.Select(dep => dep.LibraryRange.Name));
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
            var hostContext = new ApplicationHostContext(
                projectDirectory: _options.Project.ProjectDirectory,
                packagesDirectory: null,
                targetFramework: _framework);

            return hostContext;
        }
    }
}