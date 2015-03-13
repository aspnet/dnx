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

        private readonly Dictionary<string, IList<Library>> _librariesByType = new Dictionary<string, IList<Library>>();

        private DependencyManager(
            Dictionary<string, IList<Library>> librariesByType)
        {
            _librariesByType = librariesByType;
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
            // Walk dependencies
            var walker = new DependencyWalker(dependencyProviders);
            var graph = walker.Walk(name, version, targetFramework);

            // Resolve conflicts
            if (!graph.TryResolveConflicts())
            {
                throw new InvalidOperationException("Failed to resolve conflicting dependencies!");
            }

            // Build the resolved dependency list
            var librariesByType = new Dictionary<string, IList<Library>>();
            graph.ForEach(node =>
            {
                // Everything should be Accepted or Rejected by now
                Debug.Assert(node.Disposition != Disposition.Acceptable);

                if (node.Disposition == Disposition.Accepted)
                {
                    var library = node.Item.Data;

                    // Add the library to the set
                    librariesByType.GetOrAdd(library.Identity.Type, s => new List<Library>())
                        .Add(library);
                }
            });

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
            return new DependencyManager(librariesByType);
        }

        /// <summary>
        /// Get a list of all libraries matching the specified type
        /// </summary>
        /// <param name="type">The type of libraries to find (see <see cref="LibraryTypes"/> for a list of known values)</param>
        /// <returns></returns>
        public IEnumerable<Library> GetLibraries(string type)
        {
            IList<Library> libraries;
            if(!_librariesByType.TryGetValue(type, out libraries))
            {
                return Enumerable.Empty<Library>();
            }
            return libraries;
        }
    }
}