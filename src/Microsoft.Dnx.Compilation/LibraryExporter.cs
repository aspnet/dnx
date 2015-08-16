// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation
{
    public class LibraryExporter : ILibraryExporter
    {
        private readonly LibraryManager _manager;
        private readonly CompilationEngine _compilationEngine;
        private readonly FrameworkName _targetFramework;
        private readonly string _configuration;

        public LibraryExporter(LibraryManager manager, CompilationEngine compilationEngine, FrameworkName targetFramework, string configuration)
        {
            _manager = manager;
            _compilationEngine = compilationEngine;
            _targetFramework = targetFramework;
            _configuration = configuration;
        }

        public LibraryExport GetExport(string name)
        {
            return GetExport(name, aspect: null);
        }

        public LibraryExport GetExport(string name, string aspect)
        {
            var library = _manager.GetLibraryDescription(name);
            if (library == null)
            {
                return null;
            }
            return GetExport(library, aspect);
        }

        public LibraryExport GetAllExports(string name)
        {
            return GetAllExports(name, aspect: null);
        }

        public LibraryExport GetAllExports(string name, string aspect)
        {
            return GetAllExports(name, aspect, l => true);
        }

        public LibraryExport GetNonProjectExports(string name)
        {
            return GetAllExports(
                name,
                aspect: null,
                libraryFilter: l => l.Type != LibraryTypes.Project);
        }

        public LibraryExport GetAllDependencies(
            string name,
            string aspect)
        {
            return GetAllExports(name, aspect, library => !string.Equals(name, library.Name));
        }

        public LibraryExport GetAllExports(
            string name,
            string aspect,
            Func<Library, bool> libraryFilter)
        {
            var library = _manager.GetLibraryDescription(name);
            if (library == null)
            {
                return null;
            }
            return GetAllExports(library, aspect, libraryFilter);
        }

        private LibraryExport GetAllExports(
            LibraryDescription projectLibrary,
            string aspect,
            Func<Library, bool> include)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolving references for '{projectLibrary.Identity.Name}' {aspect}");

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            // Walk the dependency tree and resolve the library export for all references to this project
            var queue = new Queue<Node>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootNode = new Node
            {
                Library = _manager.GetLibrary(projectLibrary.Identity.Name)
            };

            queue.Enqueue(rootNode);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                // Skip it if we've already seen it
                if (!processed.Add(node.Library.Name))
                {
                    continue;
                }

                if (include(node.Library))
                {
                    var libraryExport = GetExport(node.Library.Name);
                    if (libraryExport != null)
                    {
                        if (node.Parent == rootNode)
                        {
                            // Only export sources from first level dependencies
                            ProcessExport(libraryExport, references, sourceReferences);
                        }
                        else
                        {
                            // Skip source exports from anything else
                            ProcessExport(libraryExport, references, sourceReferences: null);
                        }
                    }
                }

                foreach (var dependency in node.Library.Dependencies)
                {
                    var childNode = new Node
                    {
                        Library = _manager.GetLibrary(dependency),
                        Parent = node
                    };

                    queue.Enqueue(childNode);
                }
            }

            dependencyStopWatch.Stop();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolved {references.Count} references for '{projectLibrary.Identity.Name}' in {dependencyStopWatch.ElapsedMilliseconds}ms");

            return new LibraryExport(
                references.Values.ToList(),
                sourceReferences.Values.ToList());
        }

        private void ProcessExport(LibraryExport export,
                                          IDictionary<string, IMetadataReference> metadataReferences,
                                          IDictionary<string, ISourceReference> sourceReferences)
        {
            var references = new List<IMetadataReference>(export.MetadataReferences);

            foreach (var reference in references)
            {
                metadataReferences[reference.Name] = reference;
            }

            if (sourceReferences != null)
            {
                foreach (var sourceReference in export.SourceReferences)
                {
                    sourceReferences[sourceReference.Name] = sourceReference;
                }
            }
        }

        private LibraryExport GetExport(LibraryDescription library, string aspect)
        {
            // Don't even try to export unresolved libraries
            if(!library.Resolved)
            {
                return null;
            }

            if (string.Equals(LibraryTypes.Package, library.Type, StringComparison.Ordinal))
            {
                return ExportPackage((PackageDescription)library);
            }
            else if (string.Equals(LibraryTypes.Project, library.Type, StringComparison.Ordinal))
            {
                return ExportProject((ProjectDescription)library, aspect);
            }
            else
            {
                return ExportAssemblyLibrary(library);
            }
        }

        private LibraryExport ExportPackage(LibraryDescription library)
        {
            var packageLibrary = (PackageDescription)library;

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);

            if (!TryPopulateMetadataReferences(packageLibrary, references))
            {
                return null;
            }

            // REVIEW: This requires more design
            var sourceReferences = new List<ISourceReference>();

            foreach (var sharedSource in GetSharedSources(packageLibrary))
            {
                sourceReferences.Add(new SourceFileReference(sharedSource));
            }

            return new LibraryExport(references.Values.ToList(), sourceReferences);
        }

        private LibraryExport ExportProject(LibraryDescription library, string aspect)
        {
            return ProjectExporter.ExportProject(
                ((ProjectDescription)library).Project,
                _compilationEngine,
                aspect,
                _targetFramework,
                _configuration);
        }

        private LibraryExport ExportAssemblyLibrary(LibraryDescription library)
        {
            if (string.IsNullOrEmpty(library.Path))
            {
                Logger.TraceError($"[{nameof(LibraryExporter)}] Failed to export: {library.Identity.Name}");
                return null;
            }

            // We assume the path is to an assembly 
            return new LibraryExport(new MetadataFileReference(library.Identity.Name, library.Path));
        }

        private IEnumerable<string> GetSharedSources(PackageDescription package)
        {
            var directory = Path.Combine(package.Path, "shared");

            return package
                .Library
                .Files
                .Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                .Select(path => Path.Combine(package.Path, path));
        }


        private bool TryPopulateMetadataReferences(PackageDescription package, IDictionary<string, IMetadataReference> paths)
        {
            foreach (var assemblyPath in package.Target.CompileTimeAssemblies)
            {
                if (PackageDependencyProvider.IsPlaceholderFile(assemblyPath))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(assemblyPath);
                var path = Path.Combine(package.Path, assemblyPath);
                paths[name] = new MetadataFileReference(name, path);
            }

            return true;
        }

        private class Node
        {
            public Library Library { get; set; }

            public Node Parent { get; set; }
        }

    }
}
