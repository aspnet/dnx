using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly Dictionary<string, Library> _librariesByName = new Dictionary<string, Library>();
        private readonly Dictionary<string, ISet<Library>> _librariesByType = new Dictionary<string, ISet<Library>>();

        private DependencyManager(
            GraphNode<Library> graph,
            Dictionary<string, Library> librariesByName,
            Dictionary<string, ISet<Library>> librariesByType)
        {
            _graph = graph;
            _librariesByName = librariesByName;
            _librariesByType = librariesByType;
        }

        /// <summary>
        /// Tries to retrieve the library with the specified name and type.
        /// </summary>
        /// <param name="name">The name of the library to retrieve</param>
        /// <param name="library">Receives the library if the operation was successful</param>
        /// <returns>true if the library could be found, false if not</returns>
        public bool TryGetLibrary(string name, out Library library)
        {
            return _librariesByName.TryGetValue(name, out library);
        }

        // Not optimized yet. Definitely could use some caching :)
        public IEnumerable<Library> EnumerateAllDependencies(Library library)
        {
            foreach(var dependency in library.Dependencies)
            {
                Library dependencyLib;
                if(TryGetLibrary(dependency.Name, out dependencyLib))
                {
                    yield return dependencyLib;
                    foreach(var subdependency in EnumerateAllDependencies(dependencyLib))
                    {
                        yield return subdependency;
                    }
                }
            }
        }


        /// <summary>
        /// The dependency graph for the specified project and returns a <see cref="DependencyManager"/>
        /// containing the full set of resolved dependencies
        /// </summary>
        /// <param name="dependencyProviders">The <see cref="IDependencyProvider"/> objects to use to locate dependencies</param>
        /// <param name="name">The name of the root dependency to resolve</param>
        /// <param name="version">The version of the root dependency to resolve</param>
        /// <param name="targetFramework">The target framework of the root dependency to resolve</param>
        /// <returns></returns>
        public static DependencyManager ResolveDependencies(
            IEnumerable<IDependencyProvider> dependencyProviders,
            string name,
            NuGetVersion version,
            NuGetFramework targetFramework)
        {
            GraphNode<Library> graph;
            var librariesByType = new Dictionary<string, ISet<Library>>();
            var librariesByName = new Dictionary<string, Library>();
            var libraries = new Dictionary<string, Library>();
            using (Log.LogTimedMethod())
            {
                // Walk dependencies
                var walker = new DependencyWalker(dependencyProviders);
                graph = walker.Walk(name, version, targetFramework);

                // Resolve conflicts
                if (!graph.TryResolveConflicts())
                {
                    throw new InvalidOperationException("Failed to resolve conflicting dependencies!");
                }

                // Build the resolved dependency list
                graph.ForEach(node =>
                {
                    // Everything should be Accepted or Rejected by now
                    Debug.Assert(node.Disposition != Disposition.Acceptable);

                    if (node.Disposition == Disposition.Accepted)
                    {
                        var library = node.Item.Data;

                        // Add the library to the sets
                        librariesByName[node.Item.Data.Identity.Name] = node.Item.Data;
                        librariesByType.GetOrAdd(library.Identity.Type, 
                            s => new HashSet<Library>(Library.IdentityComparer))
                                .Add(library);
                    }
                });
            }

            // Write the graph
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Dependency Graph:");
                if (graph == null || graph.Item == null)
                {
                    Log.LogDebug(" <no dependencies>");
                }
                else
                {
                    graph.Dump(s => Log.LogDebug($" {s}"));
                }
                Log.LogDebug("Selected Dependencies");
                foreach (var library in librariesByType.Values.SelectMany(l => l))
                {
                    Log.LogDebug($" {library.Identity}");
                }
            }

            // Return the assembled dependency manager
            return new DependencyManager(graph, librariesByName, librariesByType);
        }

        /// <summary>
        /// Get a list of all libraries matching the specified type
        /// </summary>
        /// <param name="type">The type of libraries to find (see <see cref="LibraryTypes"/> for a list of known values)</param>
        /// <returns></returns>
        public IEnumerable<Library> GetLibraries(string type)
        {
            ISet<Library> libraries;
            if (!_librariesByType.TryGetValue(type, out libraries))
            {
                return Enumerable.Empty<Library>();
            }
            return libraries;
        }
    }
}
