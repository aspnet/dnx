// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet;
using TempRepack.Engine.Model;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreOperations
    {
        private readonly IReport _report;

        public RestoreOperations(IReport report)
        {
            _report = report;
        }

        public async Task<GraphNode> CreateGraphNode(RestoreContext context, LibraryRange libraryRange, Func<string, bool> predicate)
        {
            var sw = new Stopwatch();
            sw.Start();

            var node = new GraphNode
            {
                LibraryRange = libraryRange,
                Item = await FindLibraryCached(context, libraryRange),
            };

            if (node.Item != null)
            {
                if (node.LibraryRange.VersionRange != null &&
                    node.LibraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None)
                {
                    lock (context.FindLibraryCache)
                    {
                        if (!context.FindLibraryCache.ContainsKey(node.LibraryRange))
                        {
                            context.FindLibraryCache[node.LibraryRange] = Task.FromResult(node.Item);
                        }
                    }
                }

                var tasks = new List<Task<GraphNode>>();
                var dependencies = node.Item.Dependencies ?? Enumerable.Empty<LibraryDependency>();
                foreach (var dependency in dependencies)
                {
                    if (predicate(dependency.Name))
                    {
                        tasks.Add(CreateGraphNode(context, dependency.LibraryRange, ChainPredicate(predicate, node.Item, dependency)));

                        if (context.RuntimeSpecs != null)
                        {
                            foreach (var runtimeSpec in context.RuntimeSpecs)
                            {
                                DependencySpec dependencyMapping;
                                if (runtimeSpec.Dependencies.TryGetValue(dependency.Name, out dependencyMapping))
                                {
                                    foreach (var dependencyImplementation in dependencyMapping.Implementations.Values)
                                    {
                                        tasks.Add(CreateGraphNode(
                                            context,
                                            new LibraryRange
                                            {
                                                Name = dependencyImplementation.Name,
                                                VersionRange = VersionUtility.ParseVersionRange(dependencyImplementation.Version)
                                            },
                                            ChainPredicate(predicate, node.Item, dependency)));
                                    }
                                }
                                break;
                            }
                        }
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

        public Task<GraphItem> FindLibraryCached(RestoreContext context, LibraryRange libraryRange)
        {
            lock (context.FindLibraryCache)
            {
                Task<GraphItem> task;
                if (!context.FindLibraryCache.TryGetValue(libraryRange, out task))
                {
                    task = FindLibraryEntry(context, libraryRange);
                    context.FindLibraryCache[libraryRange] = task;
                }

                return task;
            }
        }

        private async Task<GraphItem> FindLibraryEntry(RestoreContext context, LibraryRange libraryRange)
        {
            _report.WriteLine(string.Format("Attempting to resolve dependency {0} {1}", libraryRange.Name.Bold(), libraryRange.VersionRange));

            var match = await FindLibraryMatch(context, libraryRange);

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

        private async Task<WalkProviderMatch> FindLibraryMatch(RestoreContext context, LibraryRange libraryRange)
        {
            var projectMatch = await FindProjectMatch(context, libraryRange.Name);

            if (projectMatch != null)
            {
                return projectMatch;
            }

            if (libraryRange.VersionRange == null)
            {
                return null;
            }

            if (libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            if (libraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None)
            {
                // For snapshot dependencies, get the version remotely first.
                var remoteMatch = await FindLibraryByVersion(context, libraryRange, context.RemoteLibraryProviders);
                if (remoteMatch == null)
                {
                    // If there was nothing remotely, use the local match (if any)
                    var localMatch = await FindLibraryByVersion(context, libraryRange, context.LocalLibraryProviders);
                    return localMatch;
                }
                else
                {
                    // Now check the local repository
                    var localMatch = await FindLibraryByVersion(context, libraryRange, context.LocalLibraryProviders);

                    if (localMatch != null && remoteMatch != null)
                    {
                        // We found a match locally and remotely, so pick the better version
                        // in relation to the specified version.
                        if (VersionUtility.ShouldUseConsidering(
                            current: remoteMatch.Library.Version,
                            considering: localMatch.Library.Version,
                            ideal: libraryRange.VersionRange))
                        {
                            return localMatch;
                        }

                        // The remote match is better
                    }

                    // Try to see if the specific version found on the remote exists locally. This avoids any unnecessary
                    // remote access incase we already have it in the cache/local packages folder.
                    localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);

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
                var localMatch = await FindLibraryByVersion(context, libraryRange, context.LocalLibraryProviders);

                if (localMatch != null && localMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
                {
                    // We have an exact match so use it.
                    return localMatch;
                }

                // Either we found a local match but it wasn't the exact version, or 
                // we didn't find a local match.
                var remoteMatch = await FindLibraryByVersion(context, libraryRange, context.RemoteLibraryProviders);

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
                        ideal: libraryRange.VersionRange))
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
            var libraryRange = new LibraryRange
            {
                Name = name
            };

            foreach (var provider in context.ProjectLibraryProviders)
            {
                var match = await provider.FindLibrary(libraryRange, context.FrameworkName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private async Task<WalkProviderMatch> FindLibraryByVersion(RestoreContext context, LibraryRange libraryRange, IEnumerable<IWalkProvider> providers)
        {
            if (libraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None)
            {
                // Don't optimize the non http path for floating versions or we'll miss things
                return await FindLibrary(libraryRange, providers, provider => provider.FindLibrary(libraryRange, context.FrameworkName));
            }

            // Try the non http sources first
            var nonHttpMatch = await FindLibrary(libraryRange, providers.Where(p => !p.IsHttp), provider => provider.FindLibrary(libraryRange, context.FrameworkName));

            // If we found an exact match then use it
            if (nonHttpMatch != null && nonHttpMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
            {
                return nonHttpMatch;
            }

            // Otherwise try the http sources
            var httpMatch = await FindLibrary(libraryRange, providers.Where(p => p.IsHttp), provider => provider.FindLibrary(libraryRange, context.FrameworkName));

            // Pick the best match of the 2
            if (VersionUtility.ShouldUseConsidering(
                nonHttpMatch?.Library?.Version,
                httpMatch?.Library.Version,
                libraryRange.VersionRange))
            {
                return httpMatch;
            }

            return nonHttpMatch;
        }

        private static async Task<WalkProviderMatch> FindLibrary(
            LibraryRange libraryRange,
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
                    ideal: libraryRange.VersionRange))
                {
                    bestMatch = match;
                }
            }

            return bestMatch;
        }
    }
}