// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime;
using NuGet;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreOperations
    {
        private readonly IReport _report;

        public RestoreOperations(IReport report)
        {
            _report = report;
        }

        public async Task<GraphNode> CreateGraphNode(RestoreContext context, Library library, Func<string, bool> predicate)
        {
            var sw = new Stopwatch();
            sw.Start();

            var node = new GraphNode
            {
                Library = library,
                Item = await FindLibraryCached(context, library),
            };

            if (node.Item != null)
            {
                if (node.Library != null &&
                    node.Library.Version != null &&
                    node.Library.Version.IsSnapshot)
                {
                    node.Library = node.Item.Match.Library;
                    lock (context.FindLibraryCache)
                    {
                        if (!context.FindLibraryCache.ContainsKey(node.Library))
                        {
                            context.FindLibraryCache[node.Library] = Task.FromResult(node.Item);
                        }
                    }
                }

                var tasks = new List<Task<GraphNode>>();
                var dependencies = node.Item.Dependencies ?? Enumerable.Empty<LibraryDependency>();
                foreach (var dependency in dependencies)
                {
                    if (predicate(dependency.Name))
                    {
                        tasks.Add(CreateGraphNode(context, dependency.Library, ChainPredicate(predicate, node.Item, dependency)));
                    }
                }
                while (tasks.Any())
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                    var dependency = await task;
                    node.Dependencies.Add(dependency);
                }
            }
            return node;
        }

        Func<string, bool> ChainPredicate(Func<string, bool> predicate, GraphItem item, LibraryDependency dependency)
        {
            return name =>
            {
                if (item.Match.Library.Name == name)
                {
                    throw new Exception(string.Format("TODO: Circular dependency references not supported. Package '{0}'.", name));
                }
                if (item.Dependencies.Any(d => d != dependency && d.Name == name))
                {
                    return false;
                }
                return predicate(name);
            };
        }

        public Task<GraphItem> FindLibraryCached(RestoreContext context, Library library)
        {
            lock (context.FindLibraryCache)
            {
                Task<GraphItem> task;
                if (!context.FindLibraryCache.TryGetValue(library, out task))
                {
                    task = FindLibraryEntry(context, library);
                    context.FindLibraryCache[library] = task;
                }
                return task;
            }
        }

        public async Task<GraphItem> FindLibraryEntry(RestoreContext context, Library library)
        {
            _report.WriteLine(string.Format("Attempting to resolve dependency {0} >= {1}", library.Name.Bold(), library.Version));

            var match = await FindLibraryMatch(context, library);
            if (match == null)
            {
                //                Report.WriteLine(string.Format("Unable to find '{1}' of package '{0}'", library.Name, library.Version));
                return null;
            }

            var dependencies = await match.Provider.GetDependencies(match, context.FrameworkName);

            //Report.WriteLine(string.Format("Resolved {0} {1}", match.Library.Name, match.Library.Version));

            return new GraphItem
            {
                Match = match,
                Dependencies = dependencies,
            };
        }

        public async Task<WalkProviderMatch> FindLibraryMatch(RestoreContext context, Library library)
        {
            var projectMatch = await FindLibraryByName(context, library.Name, context.ProjectLibraryProviders);
            if (projectMatch != null)
            {
                return projectMatch;
            }

            if (library.Version == null)
            {
                return null;
            }

            if (library.IsGacOrFrameworkReference)
            {
                return null;
            }

            if (library.Version.IsSnapshot)
            {
                var remoteMatch = await FindLibraryBySnapshot(context, library, context.RemoteLibraryProviders);
                if (remoteMatch == null)
                {
                    var localMatch = await FindLibraryBySnapshot(context, library, context.LocalLibraryProviders);
                    return localMatch;
                }
                else
                {
                    var localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);
                    if (localMatch != null && localMatch.Library.Version.Equals(remoteMatch.Library.Version))
                    {
                        return localMatch;
                    }
                    return remoteMatch;
                }
            }
            else
            {
                var localMatch = await FindLibraryByVersion(context, library, context.LocalLibraryProviders);
                if (localMatch != null && localMatch.Library.Version.Equals(library.Version))
                {
                    return localMatch;
                }

                var remoteMatch = await FindLibraryByVersion(context, library, context.RemoteLibraryProviders);
                if (remoteMatch != null && localMatch == null)
                {
                    localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);
                }
                if (localMatch != null && remoteMatch != null)
                {
                    if (VersionUtility.ShouldUseConsidering(
                        current: localMatch.Library.Version,
                        considering: remoteMatch.Library.Version,
                        ideal: library.Version))
                    {
                        return remoteMatch;
                    }
                    else
                    {
                        return localMatch;
                    }
                }

                return localMatch ?? remoteMatch;
            }
        }

        public async Task<WalkProviderMatch> FindLibraryByName(RestoreContext context, string name, IEnumerable<IWalkProvider> providers)
        {
            foreach (var provider in providers)
            {
                var match = await provider.FindLibraryByName(name, context.FrameworkName);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private async Task<WalkProviderMatch> FindLibraryBySnapshot(RestoreContext context, Library library, IEnumerable<IWalkProvider> providers)
        {
            List<Task<WalkProviderMatch>> tasks = new List<Task<WalkProviderMatch>>();
            foreach (var provider in providers)
            {
                tasks.Add(provider.FindLibraryBySnapshot(library, context.FrameworkName));
            }
            var matches = await Task.WhenAll(tasks);
            WalkProviderMatch bestMatch = null;
            foreach (var match in matches)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: (bestMatch == null || bestMatch.Library == null) ? null : bestMatch.Library.Version,
                    considering: (match == null || match.Library == null) ? null : match.Library.Version,
                    ideal: library.Version))
                {
                    bestMatch = match;
                }
            }
            return bestMatch;
        }

        private async Task<WalkProviderMatch> FindLibraryByVersion(RestoreContext context, Library library, IEnumerable<IWalkProvider> providers)
        {
            List<Task<WalkProviderMatch>> tasks = new List<Task<WalkProviderMatch>>();
            foreach (var provider in providers)
            {
                tasks.Add(provider.FindLibraryByVersion(library, context.FrameworkName));
            }
            var matches = await Task.WhenAll(tasks);
            WalkProviderMatch bestMatch = null;
            foreach (var match in matches)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: (bestMatch == null || bestMatch.Library == null) ? null : bestMatch.Library.Version,
                    considering: (match == null || match.Library == null) ? null : match.Library.Version,
                    ideal: library.Version))
                {
                    bestMatch = match;
                }
            }
            return bestMatch;
        }
    }

}