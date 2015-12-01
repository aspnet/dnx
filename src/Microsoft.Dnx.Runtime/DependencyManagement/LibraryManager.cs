// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Internals;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    // REVIEW(anurse): This is now a fairly simple wrapper around a collection of libraries. Rename to LibrarySet or LibraryCollection?
    // REVIEW(anurse): This could also be much more lazy. Some consumers only use the RuntimeLibrary graph, some need the Library graph.
    public class LibraryManager : ILibraryManager
    {
        private IList<LibraryDescription> _libraries;
        private IList<DiagnosticMessage> _diagnostics;

        private readonly object _initializeLock = new object();
        private Dictionary<string, IEnumerable<Library>> _inverse;
        private Dictionary<string, Tuple<Library, LibraryDescription>> _graph;
        private readonly string _projectPath;
        private readonly FrameworkName _targetFramework;

        public LibraryManager(string projectPath, FrameworkName targetFramework, IList<LibraryDescription> libraries)
        {
            _projectPath = projectPath;
            _targetFramework = targetFramework;
            _libraries = libraries;
        }

        public void AddGlobalDiagnostics(DiagnosticMessage message)
        {
            if (_diagnostics == null)
            {
                _diagnostics = new List<DiagnosticMessage>();
            }

            _diagnostics.Add(message);
        }

        private Dictionary<string, Tuple<Library, LibraryDescription>> Graph
        {
            get
            {
                EnsureGraph();
                return _graph;
            }
        }

        private Dictionary<string, IEnumerable<Library>> InverseGraph
        {
            get
            {
                EnsureInverseGraph();
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
            EnsureGraph();
            return _graph.Values.Select(l => l.Item1);
        }

        public IEnumerable<LibraryDescription> GetLibraryDescriptions()
        {
            EnsureGraph();
            return _graph.Values.Select(l => l.Item2);
        }

        public IList<DiagnosticMessage> GetAllDiagnostics()
        {
            var messages = new List<DiagnosticMessage>();

            if (_diagnostics != null)
            {
                messages.AddRange(_diagnostics);
            }

            foreach (var library in GetLibraryDescriptions())
            {
                string projectPath = library.RequestedRange.FileName ?? _projectPath;

                if (!library.Resolved)
                {
                    string message;
                    string errorCode;
                    if (library.Compatible)
                    {
                        errorCode = DiagnosticMonikers.NU1001;
                        message = $"The dependency {library.RequestedRange} could not be resolved.";
                    }
                    else
                    {
                        errorCode = DiagnosticMonikers.NU1002;
                        var projectName = Directory.GetParent(_projectPath).Name;
                        message = $"The dependency {library.Identity} in project {projectName} does not support framework {library.Framework}.";
                    }

                    messages.Add(
                        new DiagnosticMessage(
                            errorCode,
                            message,
                            projectPath,
                            DiagnosticMessageSeverity.Error,
                            library.RequestedRange.Line,
                            library.RequestedRange.Column,
                            library));
                }
                else
                {
                    // Skip libraries that aren't specified in a project.json
                    if (string.IsNullOrEmpty(library.RequestedRange.FileName))
                    {
                        continue;
                    }

                    if (library.RequestedRange.VersionRange == null)
                    {
                        // TODO: Show errors/warnings for things without versions
                        continue;
                    }

                    // If we ended up with a declared version that isn't what was asked for directly
                    // then report a warning
                    // Case 1: Non floating version and the minimum doesn't match what was specified
                    // Case 2: Floating version that fell outside of the range
                    if ((library.RequestedRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                         library.RequestedRange.VersionRange.MinVersion != library.Identity.Version) ||
                        (library.RequestedRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None &&
                         !library.RequestedRange.VersionRange.EqualsFloating(library.Identity.Version)))
                    {
                        var message = string.Format("Dependency specified was {0} but ended up with {1}.", library.RequestedRange, library.Identity);
                        messages.Add(
                            new DiagnosticMessage(
                                DiagnosticMonikers.NU1007,
                                message,
                                projectPath,
                                DiagnosticMessageSeverity.Warning,
                                library.RequestedRange.Line,
                                library.RequestedRange.Column,
                                library));
                    }
                }
            }

            return messages;
        }

        private void EnsureGraph()
        {
            lock (_initializeLock)
            {
                if (_graph == null)
                {
                    _graph = _libraries.ToDictionary(l => l.Identity.Name, l => Tuple.Create(l.ToLibrary(), l), StringComparer.Ordinal);
                    _libraries = null;
                }
            }
        }

        private void EnsureInverseGraph()
        {
            EnsureGraph();

            lock (_initializeLock)
            {
                if (_inverse == null)
                {
                    BuildInverseGraph();
                }
            }
        }

        private void BuildInverseGraph()
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