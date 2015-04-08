// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public AssemblyWalker(
            FrameworkName framework,
            ApplicationHostContext hostContext,
            string runtimeFolder)
        {
            _framework = framework;
            _hostContext = hostContext;
            _runtimeFolder = runtimeFolder;
        }

        public ISet<string> Walk(IGraphNode<LibraryDescription> root)
        {
            var assemblies = new HashSet<string>();
            var libraries = new HashSet<LibraryDescription>();
            root.DepthFirstPreOrderWalk(visitNode: (node, _) => VisitLibrary(node, _, libraries, assemblies));

            return assemblies;
        }

        private bool VisitLibrary(IGraphNode<LibraryDescription> node,
                                  IEnumerable<IGraphNode<LibraryDescription>> ancestors,
                                  ISet<LibraryDescription> visitedLibraries,
                                  ISet<string> assemblies)
        {
            if (visitedLibraries.Add(node.Item))
            {
                foreach (var loadableAssembly in node.Item.LoadableAssemblies)
                {
                    DepthFirstGraphTraversal.PreOrderWalk(
                        root: loadableAssembly,
                        visitNode: (assemblyNode, assemblyAncestors) => VisitAssembly(assemblyNode, assemblyAncestors, assemblies),
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
                return assemblyInfo.GetDependencies();
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }

        private bool VisitAssembly(string assembly, IEnumerable<string> ancestors, ISet<string> assemblies)
        {
            // determine if keep walking down the path
            if (ancestors.Any(a => a == assembly))
            {
                // break the reference loop
                return false;
            }

            var filepath = ResolveAssemblyFilePath(assembly);
            if (filepath == null)
            {
                return false;
            }

            filepath = Path.GetFullPath(filepath);
            assemblies.Add(filepath);

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
    }
}
