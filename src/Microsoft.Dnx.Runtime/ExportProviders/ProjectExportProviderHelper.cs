// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime
{
    public static class ProjectExportProviderHelper
    {
        public static LibraryExport GetExportsRecursive(
            ILibraryManager manager,
            ILibraryExportProvider libraryExportProvider,
            CompilationTarget target,
            bool dependenciesOnly)
        {
            return GetExportsRecursive(manager, libraryExportProvider, target, libraryInformation =>
            {
                if (dependenciesOnly)
                {
                    return !string.Equals(target.Name, libraryInformation.Name);
                }

                return true;
            });
        }

        public static LibraryExport GetExportsRecursive(
            ILibraryManager manager,
            ILibraryExportProvider libraryExportProvider,
            CompilationTarget target,
            Func<Library, bool> include)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Logger.TraceInformation("[{0}]: Resolving references for '{1}' {2}", typeof(ProjectExportProviderHelper).Name, target.Name, target.Aspect);

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            // Walk the dependency tree and resolve the library export for all references to this project
            var queue = new Queue<Node>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootNode = new Node
            {
                Library = manager.GetLibraryInformation(target.Name)
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
                    var libraryExport = libraryExportProvider.GetLibraryExport(target
                        .ChangeName(node.Library.Name)
                        .ChangeAspect(null));

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
                        Library = manager.GetLibraryInformation(dependency),
                        Parent = node
                    };

                    queue.Enqueue(childNode);
                }
            }

            dependencyStopWatch.Stop();
            Logger.TraceInformation("[{0}]: Resolved {1} references for '{2}' in {3}ms",
                                  typeof(ProjectExportProviderHelper).Name,
                                  references.Count,
                                  target.Name,
                                  dependencyStopWatch.ElapsedMilliseconds);

            return new LibraryExport(
                references.Values.ToList(),
                sourceReferences.Values.ToList());
        }

        private static void ProcessExport(LibraryExport export,
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

        private class Node
        {
            public Library Library { get; set; }

            public Node Parent { get; set; }
        }
    }
}
