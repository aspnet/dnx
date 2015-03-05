using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Internal;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime.Dependencies
{
    /// <summary>
    /// Represents the dependency graph for a project
    /// </summary>
    public class DependencyManager
    {
        private static readonly ILogger Log = RuntimeLogging.Logger<DependencyManager>();

        private readonly GraphNode<Library> _graph;
        private readonly Dictionary<LibraryIdentity, Library> _libraries;
        private readonly Dictionary<string, Library> _nameMap;

        public DependencyManager(GraphNode<Library> graph, Dictionary<string, Library> nameMap, Dictionary<LibraryIdentity, Library> libraries)
        {
            _graph = graph;
            _nameMap = nameMap;
            _libraries = libraries;
        }

        public static DependencyManager ResolveDependencies(
            IEnumerable<IDependencyProvider> dependencyProviders,
            string projectName,
            NuGetVersion version,
            NuGetFramework targetFramework)
        {
            // Walk dependencies
            var walker = new DependencyWalker(dependencyProviders);
            var graph = walker.Walk(projectName, version, targetFramework);

            // Resolve conflicts
            if (!graph.TryResolveConflicts())
            {
                throw new InvalidOperationException("Failed to resolve conflicting dependencies!");
            }

            // Build the resolved dependency list
            var nameMap = new Dictionary<string, Library>();
            var libraries = new Dictionary<LibraryIdentity, Library>();
            graph.ForEach(node =>
            {
                // Everything should be Accepted or Rejected by now
                Debug.Assert(node.Disposition != Disposition.Acceptable);

                if(node.Disposition == Disposition.Accepted)
                {
                    var library = node.Item.Data;

                    // Verify that if we've seen this library name
                    // before, it was the exact same identity
                    Debug.Assert(!nameMap.ContainsKey(library.Identity.Name) ||
                        Equals(nameMap[library.Identity.Name], library.Identity));

                    // Add the library to the set
                    libraries[library.Identity] = library;
                    nameMap[library.Identity.Name] = library;
                }
            });

            // Write the graph
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.WriteDebug("Dependency Graph:");
                if (graph == null || graph.Item == null)
                {
                    Log.WriteDebug(" <no dependencies>");
                }
                else
                {
                    graph.Dump(s => Log.WriteDebug($" {s}"));
                }
                Log.WriteDebug("Selected Dependencies");
                foreach(var library in libraries.Values)
                {
                    Log.WriteDebug($" {library.Identity}");
                }
            }

            // Return the assembled dependency manager
            return new DependencyManager(graph, nameMap, libraries);
        }
    }
}