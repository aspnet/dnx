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
        public static ILibraryExport GetProjectDependenciesExport(
            ILibraryManager manager,
            ILibraryExportProvider libraryExportProvider,
            Project project,
            FrameworkName targetFramework,
            string configuration)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Resolving exports for '{1}'", typeof(ProjectExportProviderHelper).Name, project.Name);

            var exports = new List<ILibraryExport>();

            // Walk the dependency tree and resolve the library export for all references to this project
            var stack = new Stack<ILibraryInformation>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            stack.Push(manager.GetLibraryInformation(project.Name));

            while (stack.Count > 0)
            {
                var library = stack.Pop();

                // Skip it if we've already seen it
                if (!processed.Add(library.Name))
                {
                    continue;
                }

                // Skip the root
                if (!string.Equals(library.Name, project.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var libraryExport = libraryExportProvider.GetLibraryExport(library.Name, targetFramework, configuration);

                    if (libraryExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                        Trace.TraceInformation("[{0}]: Failed to resolve dependency '{1}'", typeof(ProjectExportProviderHelper).Name, library.Name);
                    }
                    else
                    {
                        exports.Add(libraryExport);
                    }
                }

                foreach (var dependency in library.Dependencies)
                {
                    stack.Push(manager.GetLibraryInformation(dependency));
                }
            }

            dependencyStopWatch.Stop();
            Trace.TraceInformation("[{0}]: Resolved {1} exports for '{2}' in {3}ms",
                                  typeof(ProjectExportProviderHelper).Name,
                                  exports.Count,
                                  project.Name,
                                  dependencyStopWatch.ElapsedMilliseconds);

            IList<IMetadataReference> resolvedReferences;
            IList<ISourceReference> resolvedSources;

            dependencyStopWatch = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Resolving references for '{1}'", typeof(ProjectExportProviderHelper).Name, project.Name);

            ExtractReferences(exports, out resolvedReferences, out resolvedSources);

            dependencyStopWatch.Stop();
            Trace.TraceInformation("[{0}]: Resolved {1} references for '{2}' in {3}ms",
                                  typeof(ProjectExportProviderHelper).Name,
                                  resolvedReferences.Count,
                                  project.Name,
                                  dependencyStopWatch.ElapsedMilliseconds);

            return new LibraryExport(resolvedReferences, resolvedSources);
        }

        private static void ExtractReferences(List<ILibraryExport> dependencyExports,
                                              out IList<IMetadataReference> metadataReferences,
                                              out IList<ISourceReference> sourceReferences)
        {
            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);

            sourceReferences = new List<ISourceReference>();

            foreach (var export in dependencyExports)
            {
                ProcessExport(export, references, sourceReferences);
            }

            metadataReferences = references.Values.ToList();
        }

        private static void ProcessExport(ILibraryExport export,
                                          IDictionary<string, IMetadataReference> metadataReferences,
                                          IList<ISourceReference> sourceReferences)
        {
            var references = new List<IMetadataReference>(export.MetadataReferences);

            ExpandEmbeddedReferences(references);

            foreach (var reference in references)
            {
                metadataReferences[reference.Name] = reference;
            }

            foreach (var sourceReference in export.SourceReferences)
            {
                sourceReferences.Add(sourceReference);
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
