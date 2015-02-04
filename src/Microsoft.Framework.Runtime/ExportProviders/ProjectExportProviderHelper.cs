// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public static class ProjectExportProviderHelper
    {
        public static ILibraryExport GetExportsRecursive(
            ICache cache,
            ILibraryManager manager,
            ILibraryExportProvider libraryExportProvider,
            ILibraryKey target,
            bool dependenciesOnly)
        {
            return GetExportsRecursive(cache, manager, libraryExportProvider, target, libraryInformation =>
            {
                if (dependenciesOnly)
                {
                    return !string.Equals(target.Name, libraryInformation.Name);
                }

                return true;
            });
        }

        public static ILibraryExport GetExportsRecursive(
            ICache cache,
            ILibraryManager manager,
            ILibraryExportProvider libraryExportProvider,
            ILibraryKey target,
            Func<ILibraryInformation, bool> include)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Logger.TraceInformation("[{0}]: Resolving references for '{1}' {2}", typeof(ProjectExportProviderHelper).Name, target.Name, target.Aspect);

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            // Walk the dependency tree and resolve the library export for all references to this project
            var stack = new Queue<Node>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootNode = new Node
            {
                Library = manager.GetLibraryInformation(target.Name, target.Aspect)
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

                if (include(node.Library))
                {
                    var libraryExport = libraryExportProvider.GetLibraryExport(target
                        .ChangeName(node.Library.Name)
                        .ChangeAspect(null));

                    if (libraryExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                        Logger.TraceInformation("[{0}]: Failed to resolve dependency '{1}'", typeof(ProjectExportProviderHelper).Name, node.Library.Name);
                    }
                    else
                    {
                        if (node.Parent == rootNode)
                        {
                            // Only export sources from first level dependencies
                            ProcessExport(cache, libraryExport, references, sourceReferences);
                        }
                        else
                        {
                            // Skip source exports from anything else
                            ProcessExport(cache, libraryExport, references, sourceReferences: null);
                        }
                    }
                }

                foreach (var dependency in node.Library.Dependencies)
                {
                    var childNode = new Node
                    {
                        Library = manager.GetLibraryInformation(dependency, null),
                        Parent = node
                    };

                    stack.Enqueue(childNode);
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

        private static void ProcessExport(ICache cache,
                                          ILibraryExport export,
                                          IDictionary<string, IMetadataReference> metadataReferences,
                                          IDictionary<string, ISourceReference> sourceReferences)
        {
            var references = new List<IMetadataReference>(export.MetadataReferences);

            ExpandEmbeddedReferences(cache, references);

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

        private static void ExpandEmbeddedReferences(ICache cache, IList<IMetadataReference> references)
        {
            var otherReferences = new List<IMetadataReference>();

            foreach (var reference in references)
            {
                var fileReference = reference as IMetadataFileReference;

                if (fileReference != null &&
                    string.Equals(Path.GetExtension(fileReference.Path), ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    // We don't use the exact path since that might clash with another key
                    var key = "ANI_" + fileReference.Path;

                    var embeddedRefs = cache.Get<IList<IMetadataEmbeddedReference>>(key, ctx =>
                                       {
                                           ctx.Monitor(new FileWriteTimeCacheDependency(fileReference.Path));

                                           using (var fileStream = File.OpenRead(fileReference.Path))
                                           using (var reader = new PEReader(fileStream))
                                           {
                                               return reader.GetEmbeddedReferences();
                                           }
                                       });

                    otherReferences.AddRange(embeddedRefs);
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
