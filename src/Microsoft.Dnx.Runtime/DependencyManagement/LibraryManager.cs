// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dnx.Runtime
{
    // REVIEW(anurse): This is now a fairly simple wrapper around a collection of libraries. Rename to LibrarySet or LibraryCollection?
    // REVIEW(anurse): This could also be much more lazy. Some consumers only use the RuntimeLibrary graph, some need the Library graph.
    public class LibraryManager : ILibraryManager
    {
        private readonly Func<IEnumerable<LibraryDescription>> _librariesThunk;
        private readonly object _initializeLock = new object();
        private Dictionary<string, IEnumerable<Library>> _inverse;
        private Dictionary<string, Tuple<Library, LibraryDescription>> _graph;
        private bool _initialized;

        public LibraryManager(IEnumerable<LibraryDescription> libraries)
            : this(() => libraries)
        {
        }

        public LibraryManager(Func<IEnumerable<LibraryDescription>> librariesThunk)
        {
            _librariesThunk = librariesThunk;
        }

        private Dictionary<string, Tuple<Library, LibraryDescription>> Graph
        {
            get
            {
                EnsureInitialized();
                return _graph;
            }
        }

        private Dictionary<string, IEnumerable<Library>> InverseGraph
        {
            get
            {
                EnsureInitialized();
                return _inverse;
            }
        }

        public IEnumerable<Library> GetReferencingLibraries(string name)
        {
            IEnumerable<Library> libraries;
            if (InverseGraph.TryGetValue(name, out libraries))
            {
                return libraries;
            }

            return Enumerable.Empty<Library>();
        }

        public Library GetLibrary(string name)
        {
            Tuple<Library, LibraryDescription> library;
            if (Graph.TryGetValue(name, out library))
            {
                return library.Item1;
            }

            return null;
        }

        public LibraryDescription GetLibraryDescription(string name)
        {
            Tuple<Library, LibraryDescription> library;
            if (Graph.TryGetValue(name, out library))
            {
                return library.Item2;
            }

            return null;
        }

        public IEnumerable<Library> GetLibraries()
        {
            EnsureInitialized();
            return _graph.Values.Select(l => l.Item1);
        }

        private void EnsureInitialized()
        {
            lock (_initializeLock)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    var libraries = _librariesThunk();
                    _graph = libraries.ToDictionary(l => l.Identity.Name, l => Tuple.Create(l.ToLibrary(), l), StringComparer.Ordinal);

                    BuildInverseGraph();
                }
            }
        }

        public void BuildInverseGraph()
        {
            var firstLevelLookups = new Dictionary<string, List<Library>>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _graph.Values)
            {
                Visit(item.Item1, firstLevelLookups, visited);
            }

            _inverse = new Dictionary<string, IEnumerable<Library>>(StringComparer.OrdinalIgnoreCase);

            // Flatten the graph
            foreach (var item in _graph.Values)
            {
                Flatten(item.Item1, firstLevelLookups: firstLevelLookups);
            }
        }

        private void Visit(Library item,
                          Dictionary<string, List<Library>> inverse,
                          HashSet<string> visited)
        {
            if (!visited.Add(item.Name))
            {
                return;
            }

            foreach (var dependency in item.Dependencies)
            {
                List<Library> dependents;
                if (!inverse.TryGetValue(dependency, out dependents))
                {
                    dependents = new List<Library>();
                    inverse[dependency] = dependents;
                }

                dependents.Add(item);
                Visit(_graph[dependency].Item1, inverse, visited);
            }
        }

        private void Flatten(Library info,
                             Dictionary<string, List<Library>> firstLevelLookups,
                             HashSet<Library> parentDependents = null)
        {
            IEnumerable<Library> libraryDependents;
            if (!_inverse.TryGetValue(info.Name, out libraryDependents))
            {
                List<Library> firstLevelDependents;
                if (firstLevelLookups.TryGetValue(info.Name, out firstLevelDependents))
                {
                    var allDependents = new HashSet<Library>();
                    foreach (var dependent in firstLevelDependents)
                    {
                        allDependents.Add(dependent);
                        Flatten(dependent, firstLevelLookups, allDependents);
                    }
                    libraryDependents = allDependents;
                }
                else
                {
                    libraryDependents = Enumerable.Empty<Library>();
                }
                _inverse[info.Name] = libraryDependents;
            }
            AddRange(parentDependents, libraryDependents);
        }

        private static Func<IEnumerable<Library>> GetLibraryInfoThunk(IEnumerable<LibraryDescription> libraries)
        {
            return () => libraries.Select(runtimeLibrary => runtimeLibrary.ToLibrary());
        }

        private static void AddRange(HashSet<Library> source, IEnumerable<Library> values)
        {
            if (source != null)
            {
                foreach (var value in values)
                {
                    source.Add(value);
                }
            }
        }
    }
}