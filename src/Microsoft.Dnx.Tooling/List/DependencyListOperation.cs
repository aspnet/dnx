// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

            if (_options.Mismatch)
            {
                RenderMismatchDependencies(root);
                return true;
            }

            if (_options.Single != null)
            {
                RenderSingleDependency(root, _options.Single);
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

        private void RenderSingleDependency(IGraphNode<LibraryDependency> root, string dependency)
        {
            var mismatchSources = new Dictionary<LibraryRange, List<string>>();
            var sources = new Dictionary<LibraryRange, List<string>>();

            root.DepthFirstPreOrderWalk((node, ancestor) =>
            {
                if (node.Item.Library.Identity.Name == dependency)
                {
                    var library = node.Item.Library;
                    var sourcePath = string.Join(" => ", ancestor.Reverse().Select(a => $"{a.Item.Library.Identity.Name}/{a.Item.Library.Identity.Version}"));
                    var collectionToAdd = IsLibraryMismatch(node.Item) ? mismatchSources : sources;

                    List<string> paths;
                    if (!collectionToAdd.TryGetValue(node.Item.LibraryRange, out paths))
                    {
                        paths = new List<string>();
                        collectionToAdd[node.Item.LibraryRange] = paths;
                    }

                    paths.Add(sourcePath);
                }

                return true;
            });

            if (mismatchSources.Any() || sources.Any())
            {
                _options.Reports.Information.WriteLine($"\n[Target framework {_framework} ({VersionUtility.GetShortFrameworkName(_framework)})]\n");

                foreach (var each in sources)
                {
                    _options.Reports.WriteInformation($"Requested: {each.Key} *Matched");
                    foreach (var source in each.Value.OrderBy(one => one))
                    {
                        _options.Reports.WriteInformation($"\t{source}");
                    }
                    _options.Reports.WriteInformation(string.Empty);
                }

                int mismatch = 0;
                foreach (var each in mismatchSources)
                {
                    _options.Reports.WriteInformation($"Requested: {each.Key} *Mismatched");
                    foreach (var source in each.Value.Distinct().OrderBy(one => one))
                    {
                        _options.Reports.WriteInformation($"\t{source}");
                        mismatch++;
                    }
                }

                _options.Reports.WriteInformation($"Total: {mismatch} mismatched sources.");
            }
        }

        private void RenderMismatchDependencies(IGraphNode<LibraryDependency> root)
        {
            var results = new HashSet<Tuple<LibraryRange, LibraryIdentity>>();

            root.DepthFirstPreOrderWalk(
                (node, ancestors) =>
                {
                    if (IsLibraryMismatch(node.Item))
                    {
                        results.Add(Tuple.Create(node.Item.LibraryRange, node.Item.Library.Identity));
                    }

                    return true;
                });

            if (results.Any())
            {
                _options.Reports.WriteInformation($"\n[Target framework {_framework} ({VersionUtility.GetShortFrameworkName(_framework)})]\n");

                var c0 = results.Max(tuple => tuple.Item2.Name.Length) + 2;
                var c1 = results.Max(tuple => tuple.Item1.VersionRange.ToString().Length) + 2;
                var c2 = results.Max(tuple => tuple.Item2.Version.ToString().Length) + 2;
                var format = $"{{0,-{c0}}}{{1,-{c1}}}{{2,-{c2}}}";

                _options.Reports.WriteInformation(string.Format(format, "Dependency", "Requested", "Resolved"));
                _options.Reports.WriteInformation(string.Format(format, "----------", "---------", "--------"));

                foreach (var tuple in results.OrderBy(tuple => tuple.Item1.Name))
                {
                    _options.Reports.WriteInformation(string.Format(format,
                        tuple.Item1.Name, tuple.Item1.VersionRange.MinVersion.ToString(), tuple.Item2.Version));
                }
            }
        }

        private void RenderAllDependencies(IGraphNode<LibraryDependency> root)
        {
            var renderer = new LibraryDependencyFlatRenderer(_options.Details,
                                                             _options.ResultsFilter,
                                                             _options.Project.Dependencies.Select(dep => dep.LibraryRange.Name));
            var content = renderer.GetRenderContent(root);

            if (content.Any())
            {
                _options.Reports.Information.WriteLine($"\n[Target framework {_framework} ({VersionUtility.GetShortFrameworkName(_framework)})]\n");

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

        private static bool IsLibraryMismatch(LibraryDependency dependency)
        {
            if (dependency.LibraryRange?.VersionRange != null)
            {
                // If we ended up with a declared version that isn't what was asked for directly
                // then report a warning
                // Case 1: Non floating version and the minimum doesn't match what was specified
                // Case 2: Floating version that fell outside of the range
                if ((dependency.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                     dependency.LibraryRange.VersionRange.MinVersion != dependency.Library.Identity.Version) ||
                    (dependency.LibraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None &&
                     !dependency.LibraryRange.VersionRange.EqualsFloating(dependency.Library.Identity.Version)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}