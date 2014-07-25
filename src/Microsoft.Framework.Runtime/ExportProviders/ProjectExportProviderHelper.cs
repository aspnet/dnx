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
        public static ILibraryExport GetProjectDependenciesExport(ILibraryExportProvider libraryExportProvider, Project project, FrameworkName targetFramework, string configuration, out FrameworkName effectiveTargetFramework)
        {
            effectiveTargetFramework = null;

            var targetFrameworkInformation = project.GetTargetFramework(targetFramework);

            targetFramework = targetFrameworkInformation.FrameworkName ?? targetFramework;

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", typeof(ProjectExportProviderHelper).Name, project.Name, targetFramework);

            var exports = new List<ILibraryExport>();

            var dependencies = project.Dependencies.Concat(targetFrameworkInformation.Dependencies)
                                                   .Select(d => d.Name)
                                                   .ToList();

            if (VersionUtility.IsDesktop(targetFramework))
            {
                // mscorlib is ok
                dependencies.Add("mscorlib");

                // TODO: Remove these references
                dependencies.Add("System");
                dependencies.Add("System.Core");
                dependencies.Add("Microsoft.CSharp");
            }

            var dependencyStopWatch = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Resolving exports for '{1}'", typeof(ProjectExportProviderHelper).Name, project.Name);

            if (dependencies.Count > 0)
            {
                foreach (var dependency in dependencies)
                {
                    var libraryExport = libraryExportProvider.GetLibraryExport(dependency, targetFramework, configuration);

                    if (libraryExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                        Trace.TraceInformation("[{0}]: Failed to resolve dependency '{1}'", typeof(ProjectExportProviderHelper).Name, dependency);
                    }
                    else
                    {
                        exports.Add(libraryExport);
                    }
                }
            }

            IList<IMetadataReference> resolvedReferences;
            IList<ISourceReference> resolvedSources;

            ExtractReferences(exports,
                              libraryExportProvider,
                              targetFramework,
                              configuration,
                              out resolvedReferences,
                              out resolvedSources);

            dependencyStopWatch.Stop();
            Trace.TraceInformation("[{0}]: Resolved {1} exports for '{2}' in {3}ms",
                                  typeof(ProjectExportProviderHelper).Name,
                                  resolvedReferences.Count,
                                  project.Name,
                                  dependencyStopWatch.ElapsedMilliseconds);

            // Set the effective target framework (the specific framework used for resolution)
            effectiveTargetFramework = targetFramework;
            return new LibraryExport(resolvedReferences, resolvedSources);
        }

        private static void ExtractReferences(List<ILibraryExport> dependencyExports,
                                              ILibraryExportProvider libraryExportProvider,
                                              FrameworkName targetFramework,
                                              string configuration,
                                              out IList<IMetadataReference> metadataReferences,
                                              out IList<ISourceReference> sourceReferences)
        {
            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);

            sourceReferences = new List<ISourceReference>();

            foreach (var export in dependencyExports)
            {
                ProcessExport(export,
                              libraryExportProvider,
                              targetFramework,
                              configuration,
                              references,
                              sourceReferences);
            }

            metadataReferences = references.Values.ToList();
        }

        private static void ProcessExport(ILibraryExport export,
                                          ILibraryExportProvider libraryExportProvider,
                                          FrameworkName targetFramework,
                                          string configuration,
                                          IDictionary<string, IMetadataReference> metadataReferences,
                                          IList<ISourceReference> sourceReferences)
        {
            var references = new List<IMetadataReference>(export.MetadataReferences);

            ExpandEmbeddedReferences(references);

            foreach (var reference in references)
            {
                var unresolvedReference = reference as UnresolvedMetadataReference;

                if (unresolvedReference != null)
                {
                    // Try to resolve the unresolved references
                    var compilationExport = libraryExportProvider.GetLibraryExport(unresolvedReference.Name, targetFramework, configuration);

                    if (compilationExport != null)
                    {
                        ProcessExport(compilationExport,
                                      libraryExportProvider,
                                      targetFramework,
                                      configuration,
                                      metadataReferences,
                                      sourceReferences);
                    }
                }
                else
                {
                    metadataReferences[reference.Name] = reference;
                }
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
