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
        // TODO: Figure out caching here

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
            var stack = new Stack<ILibraryInformation>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            stack.Push(manager.GetLibraryInformation(name));

            while (stack.Count > 0)
            {
                var library = stack.Pop();

                // Skip it if we've already seen it
                if (!processed.Add(library.Name))
                {
                    continue;
                }

                bool isRoot = string.Equals(library.Name, name, StringComparison.OrdinalIgnoreCase);

                if (!dependenciesOnly || (dependenciesOnly && !isRoot))
                {
                    var libraryExport = libraryExportProvider.GetLibraryExport(library.Name, targetFramework, configuration);

                    if (libraryExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                        Trace.TraceInformation("[{0}]: Failed to resolve dependency '{1}'", typeof(ProjectExportProviderHelper).Name, library.Name);
                    }
                    else
                    {
                        ProcessExport(libraryExport, references, sourceReferences);
                    }
                }

                foreach (var dependency in library.Dependencies)
                {
                    stack.Push(manager.GetLibraryInformation(dependency));
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

            foreach (var sourceReference in export.SourceReferences)
            {
                sourceReferences[sourceReference.Name] = sourceReference;
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

    }
}
