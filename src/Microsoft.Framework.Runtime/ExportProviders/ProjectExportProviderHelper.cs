// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    internal static class ProjectExportProviderHelper
    {
        public static ILibraryExport GetExportsRecursive(
            ILibraryManager manager,
            ILibraryExportProvider libraryExportProvider,
            string name,
            FrameworkName targetFramework,
            string configuration,
            bool dependenciesOnly)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Resolving references for '{1}'", typeof(ProjectExportProviderHelper).Name, name);

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            // Walk the dependency tree and resolve the library export for all references to this project
            var stack = new Queue<Node>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootNode = new Node
            {
                Library = manager.GetLibraryInformation(name)
            };

            stack.Enqueue(rootNode);

            while (stack.Count > 0)
            {
                var node = stack.Dequeue();

                // Skip it if we've already seen it
                if (!processed.Add(node.Library.Name))
                {
                    continue;
                }

                bool isRoot = node.Parent == null;

                if (!dependenciesOnly || (dependenciesOnly && !isRoot))
                {
                    var libraryExport = libraryExportProvider.GetLibraryExport(node.Library.Name, targetFramework, configuration);

                    if (libraryExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                        Trace.TraceInformation("[{0}]: Failed to resolve dependency '{1}'", typeof(ProjectExportProviderHelper).Name, node.Library.Name);
                    }
                    else
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

                    stack.Enqueue(childNode);
                }
            }

            dependencyStopWatch.Stop();
            Trace.TraceInformation("[{0}]: Resolved {1} references for '{2}' in {3}ms",
                                  typeof(ProjectExportProviderHelper).Name,
                                  references.Count,
                                  name,
                                  dependencyStopWatch.ElapsedMilliseconds);

            return new LibraryExport(
                references.Values.ToList(),
                sourceReferences.Values.ToList());
        }

        private static void ProcessExport(ILibraryExport export,
                                          IDictionary<string, IMetadataReference> metadataReferences,
                                          IDictionary<string, ISourceReference> sourceReferences)
        {
            var references = new List<IMetadataReference>(export.MetadataReferences);

            ExpandEmbeddedReferences(references);

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

        private static void ExpandEmbeddedReferences(IList<IMetadataReference> references)
        {
            var otherReferences = new List<IMetadataReference>();

            foreach (var reference in references)
            {
                var fileReference = reference as IMetadataFileReference;

                if (fileReference != null)
                {
                    using (var fileStream = File.OpenRead(fileReference.Path))
                    using (var reader = new PEReader(fileStream))
                    {
                        otherReferences.AddRange(reader.GetEmbeddedReferences());
                    }
                }
            }

            references.AddRange(otherReferences);
        }

        private class Node
        {
            public ILibraryInformation Library { get; set; }

            public Node Parent { get; set; }
        }
    }
}
