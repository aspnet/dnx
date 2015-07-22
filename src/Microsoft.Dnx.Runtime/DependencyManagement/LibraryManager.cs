// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Caching;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryManager : ILibraryManager, ILibraryExporter
    {
        private readonly FrameworkName _targetFramework;
        private readonly string _configuration;
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly ICache _cache;
        private readonly Func<IEnumerable<Library>> _libraryInfoThunk;
        private readonly object _initializeLock = new object();
        private Dictionary<string, IEnumerable<Library>> _inverse;
        private Dictionary<string, Library> _graph;
        private bool _initialized;

        public LibraryManager(FrameworkName targetFramework,
                              string configuration,
                              DependencyWalker dependencyWalker,
                              ILibraryExportProvider libraryExportProvider,
                              ICache cache)
            : this(targetFramework,
                   configuration,
                   GetLibraryInfoThunk(dependencyWalker),
                   libraryExportProvider,
                   cache)
        {
        }

        public LibraryManager(FrameworkName targetFramework,
                              string configuration,
                              Func<IEnumerable<Library>> libraryInfoThunk,
                              ILibraryExportProvider libraryExportProvider,
                              ICache cache)
        {
            _targetFramework = targetFramework;
            _configuration = configuration;
            _libraryInfoThunk = libraryInfoThunk;
            _libraryExportProvider = libraryExportProvider;
            _cache = cache;
        }

        private Dictionary<string, Library> LibraryLookup
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

        public LibraryExport GetLibraryExport(string name)
        {
            return GetLibraryExport(name, aspect: null);
        }

        public LibraryExport GetAllExports(string name)
        {
            return GetAllExports(name, aspect: null);
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

        public Library GetLibraryInformation(string name)
        {
            Library information;
            if (LibraryLookup.TryGetValue(name, out information))
            {
                return information;
            }

            return null;
        }

        public IEnumerable<Library> GetLibraries()
        {
            EnsureInitialized();
            return _graph.Values;
        }

        public LibraryExport GetLibraryExport(string name, string aspect)
        {
            return _libraryExportProvider.GetLibraryExport(new CompilationTarget(name, _targetFramework, _configuration, aspect));
        }

        public LibraryExport GetAllExports(string name, string aspect)
        {
            var key = Tuple.Create(
                nameof(LibraryManager),
                nameof(GetAllExports),
                name,
                _targetFramework,
                _configuration,
                aspect);

            return _cache.Get<LibraryExport>(key, ctx =>
                ProjectExportProviderHelper.GetExportsRecursive(
                    this,
                    _libraryExportProvider,
                    new CompilationTarget(name, _targetFramework, _configuration, aspect),
                    dependenciesOnly: false));
        }

        private void EnsureInitialized()
        {
            lock (_initializeLock)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    _graph = _libraryInfoThunk().ToDictionary(ld => ld.Name,
                                                              StringComparer.Ordinal);

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
                Visit(item, firstLevelLookups, visited);
            }

            _inverse = new Dictionary<string, IEnumerable<Library>>(StringComparer.OrdinalIgnoreCase);

            // Flatten the graph
            foreach (var item in _graph.Values)
            {
                Flatten(item, firstLevelLookups: firstLevelLookups);
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
                Visit(_graph[dependency], inverse, visited);
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

        private static Func<IEnumerable<Library>> GetLibraryInfoThunk(DependencyWalker dependencyWalker)
        {
            return () => dependencyWalker.Libraries
                                         .Select(libraryDescription => libraryDescription.ToLibrary());
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