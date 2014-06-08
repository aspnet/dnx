// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class ProjectExportProvider
    {
        private readonly IProjectResolver _projectResolver;

        public ProjectExportProvider(IProjectResolver projectResolver)
        {
            _projectResolver = projectResolver;
        }

        public ILibraryExport GetProjectExport(ILibraryExportProvider libraryExportProvider, string name, FrameworkName targetFramework)
        {
            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            var targetFrameworkConfig = project.GetTargetFrameworkConfiguration(targetFramework);

            // Update the target framework for compilation
            targetFramework = targetFrameworkConfig.FrameworkName ?? targetFramework;

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", GetType().Name, project.Name, targetFramework);

            var exports = new List<ILibraryExport>();

            var dependencies = project.Dependencies.Concat(targetFrameworkConfig.Dependencies)
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
            Trace.TraceInformation("[{0}]: Resolving exports for '{1}'", GetType().Name, project.Name);

            if (dependencies.Count > 0)
            {
                foreach (var dependency in dependencies)
                {
                    var libraryExport = libraryExportProvider.GetLibraryExport(dependency, targetFramework);

                    if (libraryExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                        Trace.TraceInformation("[{0}]: Failed to resolve dependency '{1}'", GetType().Name, dependency);
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
                              out resolvedReferences, 
                              out resolvedSources);

            dependencyStopWatch.Stop();
            Trace.TraceInformation("[{0}]: Resolved {1} exports for '{2}' in {3}ms", 
                                  GetType().Name, 
                                  resolvedReferences.Count, 
                                  project.Name, 
                                  dependencyStopWatch.ElapsedMilliseconds);

            return new LibraryExport(resolvedReferences, resolvedSources);
        }

        private void ExtractReferences(List<ILibraryExport> dependencyExports,
                                       ILibraryExportProvider libraryExportProvider,
                                       FrameworkName targetFramework,
                                       out IList<IMetadataReference> metadataReferences,
                                       out IList<ISourceReference> sourceReferences)
        {
            var used = new HashSet<string>();
            metadataReferences = new List<IMetadataReference>();
            sourceReferences = new List<ISourceReference>();

            foreach (var export in dependencyExports)
            {
                ProcessExport(export,
                              libraryExportProvider,
                              targetFramework,
                              metadataReferences,
                              sourceReferences,
                              used);
            }
        }

        private void ProcessExport(ILibraryExport export,
                                   ILibraryExportProvider libraryExportProvider,
                                   FrameworkName targetFramework,
                                   IList<IMetadataReference> metadataReferences,
                                   IList<ISourceReference> sourceReferences,
                                   HashSet<string> used)
        {
            ExpandEmbeddedReferences(export.MetadataReferences);

            foreach (var reference in export.MetadataReferences)
            {
                var unresolvedReference = reference as UnresolvedMetadataReference;

                if (unresolvedReference != null)
                {
                    // Try to resolve the unresolved references
                    var compilationExport = libraryExportProvider.GetLibraryExport(unresolvedReference.Name, targetFramework);

                    if (compilationExport != null)
                    {
                        ProcessExport(compilationExport,
                                      libraryExportProvider,
                                      targetFramework,
                                      metadataReferences,
                                      sourceReferences,
                                      used);
                    }
                }
                else
                {
                    if (!used.Add(reference.Name))
                    {
                        continue;
                    }

                    metadataReferences.Add(reference);
                }
            }

            foreach (var sourceReference in export.SourceReferences)
            {
                sourceReferences.Add(sourceReference);
            }
        }

        private void ExpandEmbeddedReferences(IList<IMetadataReference> references)
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
