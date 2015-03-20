// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Caching;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime
{
    public class LibraryManager : ILibraryManager
    {
        private readonly FrameworkName _targetFramework;
        private readonly string _configuration;
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly ICache _cache;
        private readonly Func<IEnumerable<ILibraryInformation>> _libraryInfoThunk;
        private readonly object _initializeLock = new object();
        private Dictionary<string, IEnumerable<ILibraryInformation>> _inverse;
        private Dictionary<string, ILibraryInformation> _graph;
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
                              Func<IEnumerable<ILibraryInformation>> libraryInfoThunk,
                              ILibraryExportProvider libraryExportProvider,
                              ICache cache)
        {
            _targetFramework = targetFramework;
            _configuration = configuration;
            _libraryInfoThunk = libraryInfoThunk;
            _libraryExportProvider = libraryExportProvider;
            _cache = cache;
        }

        private Dictionary<string, ILibraryInformation> LibraryLookup
        {
            get
            {
                EnsureInitialized();
                return _graph;
            }
        }

        private Dictionary<string, IEnumerable<ILibraryInformation>> InverseGraph
        {
            get
            {
                EnsureInitialized();
                return _inverse;
            }
        }

        public ILibraryExport GetLibraryExport(string name)
        {
            return GetLibraryExport(name, aspect: null);
        }

        public ILibraryExport GetAllExports(string name)
        {
            return GetAllExports(name, aspect: null);
        }

        public IEnumerable<ILibraryInformation> GetReferencingLibraries(string name)
        {
            return GetReferencingLibraries(name, aspect: null);
        }

        public ILibraryInformation GetLibraryInformation(string name)
        {
            return GetLibraryInformation(name, aspect: null);
        }

        public IEnumerable<ILibraryInformation> GetLibraries()
        {
            return GetLibraries(aspect: null);
        }

        public ILibraryInformation GetLibraryInformation(string name, string aspect)
        {
            ILibraryInformation information;
            if (LibraryLookup.TryGetValue(name, out information))
            {
                return information;
            }

            return null;
        }

        public IEnumerable<ILibraryInformation> GetReferencingLibraries(string name, string aspect)
        {
            IEnumerable<ILibraryInformation> libraries;
            if (InverseGraph.TryGetValue(name, out libraries))
            {
                return libraries;
            }

            return Enumerable.Empty<ILibraryInformation>();
        }

        public ILibraryExport GetLibraryExport(string name, string aspect)
        {
            return _libraryExportProvider.GetLibraryExport(new LibraryKey
            {
                Name = name,
                TargetFramework = _targetFramework,
                Configuration = _configuration,
                Aspect = aspect,
            });
        }

        public ILibraryExport GetAllExports(string name, string aspect)
        {
            var key = Tuple.Create(
                nameof(LibraryManager),
                nameof(GetAllExports),
                name,
                _targetFramework,
                _configuration,
                aspect);

            return _cache.Get<ILibraryExport>(key, ctx =>
            ProjectExportProviderHelper.GetExportsRecursive(
                _cache,
                this,
                _libraryExportProvider,
                new LibraryKey
                {
                    Name = name,
                    TargetFramework = _targetFramework,
                    Configuration = _configuration,
                    Aspect = aspect,
                },
                dependenciesOnly: false)
            );
        }

        public IEnumerable<ILibraryInformation> GetLibraries(string aspect)
        {
            EnsureInitialized();
            return _graph.Values;
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
            var firstLevelLookups = new Dictionary<string, List<ILibraryInformation>>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _graph.Values)
            {
                Visit(item, firstLevelLookups, visited);
            }

            _inverse = new Dictionary<string, IEnumerable<ILibraryInformation>>(StringComparer.OrdinalIgnoreCase);

            // Flatten the graph
            foreach (var item in _graph.Values)
            {
                Flatten(item, firstLevelLookups: firstLevelLookups);
            }
        }

        private void Visit(ILibraryInformation item,
                          Dictionary<string, List<ILibraryInformation>> inverse,
                          HashSet<string> visited)
        {
            if (!visited.Add(item.Name))
            {
                return;
            }

            foreach (var dependency in item.Dependencies)
            {
                List<ILibraryInformation> dependents;
                if (!inverse.TryGetValue(dependency, out dependents))
                {
                    dependents = new List<ILibraryInformation>();
                    inverse[dependency] = dependents;
                }

                dependents.Add(item);
                Visit(_graph[dependency], inverse, visited);
            }
        }

        private void Flatten(ILibraryInformation info,
                             Dictionary<string, List<ILibraryInformation>> firstLevelLookups,
                             HashSet<ILibraryInformation> parentDependents = null)
        {
            IEnumerable<ILibraryInformation> libraryDependents;
            if (!_inverse.TryGetValue(info.Name, out libraryDependents))
            {
                List<ILibraryInformation> firstLevelDependents;
                if (firstLevelLookups.TryGetValue(info.Name, out firstLevelDependents))
                {
                    var allDependents = new HashSet<ILibraryInformation>();
                    foreach (var dependent in firstLevelDependents)
                    {
                        allDependents.Add(dependent);
                        Flatten(dependent, firstLevelLookups, allDependents);
                    }
                    libraryDependents = allDependents;
                }
                else
                {
                    libraryDependents = Enumerable.Empty<ILibraryInformation>();
                }
                _inverse[info.Name] = libraryDependents;
            }
            AddRange(parentDependents, libraryDependents);
        }

        private static Func<IEnumerable<ILibraryInformation>> GetLibraryInfoThunk(DependencyWalker dependencyWalker)
        {
            return () => dependencyWalker.Libraries
                                         .Select(library => new LibraryInformation(library));
        }

        private static void AddRange(HashSet<ILibraryInformation> source, IEnumerable<ILibraryInformation> values)
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