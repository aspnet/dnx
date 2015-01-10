// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet;

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

        private Func<string, bool> ChainPredicate(Func<string, bool> predicate, GraphItem item, LibraryDependency dependency)
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

        private async Task<GraphItem> FindLibraryEntry(RestoreContext context, Library library)
        {
            _report.WriteLine(string.Format("Attempting to resolve dependency {0} >= {1}", library.Name.Bold(), library.Version));

            var match = await FindLibraryMatch(context, library);

            if (match == null)
            {
                return null;
            }

            var dependencies = await match.Provider.GetDependencies(match, context.FrameworkName);

            return new GraphItem
            {
                Match = match,
                Dependencies = dependencies,
            };
        }

        private async Task<WalkProviderMatch> FindLibraryMatch(RestoreContext context, Library library)
        {
            var projectMatch = await FindProjectMatch(context, library.Name);

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

            // IMPORTANT: Snapshot versions end with -*
            // If you specify "A": "1.0.0-*", it means that you're looking for
            // the latest version matching the 1.0.0-* prefix OR the lowest
            // version NOT matching that prefix. As an example,
            // Let's say the following versions exist:
            // 1. A 1.0.0-beta1-0001
            // 1. A 1.0.0-beta1-0002
            // 1. A 1.0.0-beta1-0003
            // Asking for 1.0.0-beta1-* means you'll get 1.0.0-beta1-0003
            // Asking for 1.0.0-beta1 means you'll get 1.0.0-beta1-0001
            // Normal versions are minimums not exact matches. Of course
            // this means if the exact version exists, you'll get that version.


            if (library.Version.IsSnapshot)
            {
                // For snapshot dependencies, get the version remotely first.
                var remoteMatch = await FindLibraryByVersion(context, library, context.RemoteLibraryProviders);
                if (remoteMatch == null)
                {
                    // If there was nothing remotely, use the local match (if any)
                    var localMatch = await FindLibraryByVersion(context, library, context.LocalLibraryProviders);
                    return localMatch;
                }
                else
                {
                    // Try to see if the specific version found on the remote exists locally. This avoids any unnecessary
                    // remote access incase we already have it in the cache/local packages folder.
                    var localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);

                    if (localMatch != null && localMatch.Library.Version.Equals(remoteMatch.Library.Version))
                    {
                        // If we have a local match, and it matches the version *exactly* then use it.
                        return localMatch;
                    }

                    // We found something locally, but it wasn't an exact match
                    // for the resolved remote match.
                    return remoteMatch;
                }
            }
            else
            {
                // Check for the specific version locally.
                var localMatch = await FindLibraryByVersion(context, library, context.LocalLibraryProviders);

                if (localMatch != null && localMatch.Library.Version.Equals(library.Version))
                {
                    // We have an exact match so use it.
                    return localMatch;
                }

                // Either we found a local match but it wasn't the exact version, or 
                // we didn't find a local match.
                var remoteMatch = await FindLibraryByVersion(context, library, context.RemoteLibraryProviders);

                if (remoteMatch != null && localMatch == null)
                {
                    // There wasn't any local match for the specified version but there was a remote match.
                    // See if that version exists locally.
                    localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);
                }

                if (localMatch != null && remoteMatch != null)
                {
                    // We found a match locally and remotely, so pick the better version
                    // in relation to the specified version.
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

                // Prefer local over remote generally.
                return localMatch ?? remoteMatch;
            }
        }

        private async Task<WalkProviderMatch> FindProjectMatch(RestoreContext context, string name)
        {
            var library = new Library
            {
                Name = name,
                // Versions are ignored for project matches
                Version = new SemanticVersion(new Version(0, 0))
            };
            
            foreach (var provider in context.ProjectLibraryProviders)
            {
                var match = await provider.FindLibraryByVersion(library, context.FrameworkName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private async Task<WalkProviderMatch> FindLibraryByVersion(RestoreContext context, Library library, IEnumerable<IWalkProvider> providers)
        {
            if (library.Version.IsSnapshot)
            {
                // Don't optimize the non http path for snapshot versions or we'll miss things
                return await FindLibrary(library, providers, provider => provider.FindLibraryByVersion(library, context.FrameworkName));
            }

            // Try the non http sources first
            var nonHttpMatch = await FindLibrary(library, providers.Where(p => !p.IsHttp), provider => provider.FindLibraryByVersion(library, context.FrameworkName));

            // If we found an exact match then use it
            if (nonHttpMatch != null && nonHttpMatch.Library.Version.Equals(library.Version))
            {
                return nonHttpMatch;
            }

            // Otherwise try the http sources
            var httpMatch = await FindLibrary(library, providers.Where(p => p.IsHttp), provider => provider.FindLibraryByVersion(library, context.FrameworkName));

            // Pick the best match of the 2
            if (VersionUtility.ShouldUseConsidering(
                nonHttpMatch?.Library?.Version,
                httpMatch?.Library.Version,
                library.Version))
            {
                return httpMatch;
            }

            return nonHttpMatch;
        }

        private static async Task<WalkProviderMatch> FindLibrary(
            Library library,
            IEnumerable<IWalkProvider> providers,
            Func<IWalkProvider, Task<WalkProviderMatch>> action)
        {
            var tasks = new List<Task<WalkProviderMatch>>();
            foreach (var provider in providers)
            {
                tasks.Add(action(provider));
            }

            WalkProviderMatch bestMatch = null;
            var matches = await Task.WhenAll(tasks);
            foreach (var match in matches)
            {
                if (VersionUtility.ShouldUseConsidering(
                    current: bestMatch?.Library?.Version,
                    considering: match?.Library?.Version,
                    ideal: library.Version))
                {
                    bestMatch = match;
                }
            }

            return bestMatch;
        }
    }
}