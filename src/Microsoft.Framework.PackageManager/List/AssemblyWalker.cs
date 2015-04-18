// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.PackageManager.Algorithms;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.List
{
    public class AssemblyWalker
    {
        private readonly static FrameworkName AspNetCore50 = VersionUtility.ParseFrameworkName("dnxcore50");

        private readonly FrameworkName _framework;
        private readonly ApplicationHostContext _hostContext;
        private readonly string _runtimeFolder;
        private readonly bool _showDetails;
        private readonly Reports _reports;

        private HashSet<string> _assemblyFilePaths;
        private Dictionary<string, HashSet<string>> _dependencyAssemblySources;
        private Dictionary<string, HashSet<string>> _dependencyPackageSources;

        public AssemblyWalker(
            FrameworkName framework,
            ApplicationHostContext hostContext,
            string runtimeFolder,
            bool showDetails,
            Reports reports)
        {
            _framework = framework;
            _hostContext = hostContext;
            _runtimeFolder = runtimeFolder;
            _showDetails = showDetails;
            _reports = reports;
        }

        public void Walk(IGraphNode<LibraryDescription> root)
        {
            _assemblyFilePaths = new HashSet<string>(StringComparer.Ordinal);
            _dependencyAssemblySources = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            _dependencyPackageSources = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            var libraries = new HashSet<LibraryDescription>();
            root.DepthFirstPreOrderWalk(visitNode: (node, _) => VisitLibrary(node, _, libraries));

            _reports.Information.WriteLine("\n[Target framework {0} ({1})]\n",
                _framework.ToString(), VersionUtility.GetShortFrameworkName(_framework));

            foreach (var assemblyFilePath in _assemblyFilePaths.OrderBy(assemblyName => assemblyName))
            {
                _reports.Information.WriteLine(assemblyFilePath);
                if (_showDetails)
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(assemblyFilePath);

                    HashSet<string> packagesSources;
                    if (_dependencyPackageSources.TryGetValue(assemblyName, out packagesSources) && packagesSources.Any())
                    {
                        _reports.Information.WriteLine("    by package:  {0}", string.Join(", ", packagesSources));
                    }

                    HashSet<string> assemblySources;
                    if (_dependencyAssemblySources.TryGetValue(assemblyName, out assemblySources) && assemblySources.Any())
                    {
                        _reports.Information.WriteLine("    by assembly: {0}", string.Join(", ", assemblySources));
                    }
                }
            }
        }

        private bool VisitLibrary(IGraphNode<LibraryDescription> node,
                                  IEnumerable<IGraphNode<LibraryDescription>> ancestors,
                                  ISet<LibraryDescription> visitedLibraries)
        {
            if (visitedLibraries.Add(node.Item))
            {
                foreach (var loadableAssembly in node.Item.LoadableAssemblies)
                {
                    AddDependencySource(_dependencyPackageSources, loadableAssembly, node.Item.Identity.Name);

                    DepthFirstGraphTraversal.PreOrderWalk(
                        root: loadableAssembly,
                        visitNode: VisitAssembly,
                        getChildren: GetAssemblyDependencies);
                }

                return true;
            }

            return false;
        }

        private IEnumerable<string> GetAssemblyDependencies(string assemblyName)
        {
            var filepath = ResolveAssemblyFilePath(assemblyName);
            if (filepath != null && File.Exists(filepath))
            {
                var assemblyInfo = new AssemblyInformation(filepath, processorArchitecture: null);
                var dependencies = assemblyInfo.GetDependencies();

                foreach (var dependency in dependencies)
                {
                    AddDependencySource(_dependencyAssemblySources, dependency, assemblyName);
                }

                return dependencies;
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }

        private bool VisitAssembly(string assemblyName, IEnumerable<string> ancestors)
        {
            // determine if keep walking down the path
            if (ancestors.Any(a => string.Equals(a, assemblyName, StringComparison.Ordinal)))
            {
                // break the reference loop
                return false;
            }

            var filepath = ResolveAssemblyFilePath(assemblyName);
            if (filepath == null)
            {
                return false;
            }

            filepath = Path.GetFullPath(filepath);
            _assemblyFilePaths.Add(filepath);

            return true;
        }

        private string ResolveAssemblyFilePath(string assemblyName)
        {
            // Look into the CoreCLR folder first. 
            if (_runtimeFolder != null && _framework == AspNetCore50)
            {
                var coreclrAssemblyFilePath = Path.Combine(_runtimeFolder, assemblyName + ".dll");
                if (File.Exists(coreclrAssemblyFilePath))
                {
                    return coreclrAssemblyFilePath;
                }
            }

            // Look into the NuGets then.
            PackageAssembly assembly;
            if (_hostContext.NuGetDependencyProvider.PackageAssemblyLookup.TryGetValue(assemblyName, out assembly))
            {
                return assembly.Path;
            }

            return null;
        }

        private static void AddDependencySource(Dictionary<string, HashSet<string>> dependencySources, string dependency, string source)
        {
            HashSet<string> sources;
            if (!dependencySources.TryGetValue(dependency, out sources))
            {
                sources = new HashSet<string>();
                dependencySources.Add(dependency, sources);
            }

            sources.Add(source);
        }
    }
}
