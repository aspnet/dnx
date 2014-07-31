// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class ProjectLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<TypeInformation, IProjectReferenceProvider> _projectReferenceProviders = new Dictionary<TypeInformation, IProjectReferenceProvider>();
        private readonly Dictionary<string, ILibraryExport> _exportCache = new Dictionary<string, ILibraryExport>(StringComparer.OrdinalIgnoreCase);

        public ProjectLibraryExportProvider(IProjectResolver projectResolver,
                                            IServiceProvider serviceProvider)
        {
            _projectResolver = projectResolver;
            _serviceProvider = serviceProvider;
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
        {
            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            // REVIEW: This cache should probably be keyed on all the inputs. This works because
            // callers create a new environment per target framework today.

            return _exportCache.GetOrAdd(name, _ =>
            {
                // Get the composite library export provider
                var exportProvider = (ILibraryExportProvider)_serviceProvider.GetService(typeof(ILibraryExportProvider));
                var libraryManager = (ILibraryManager)_serviceProvider.GetService(typeof(ILibraryManager));

                var targetFrameworkInformation = project.GetTargetFramework(targetFramework);

                // This is the target framework defined in the project. If there were no target frameworks
                // defined then this is the targetFramework specified
                var effectiveTargetFramework = targetFrameworkInformation.FrameworkName ?? targetFramework;

                // Get the exports for the project dependencies
                ILibraryExport projectExport = ProjectExportProviderHelper.GetExportsRecursive(
                    libraryManager,
                    exportProvider,
                    project.Name,
                    effectiveTargetFramework,
                    configuration,
                    dependenciesOnly: true);

                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                if (!string.IsNullOrEmpty(targetFrameworkInformation.AssemblyPath))
                {
                    var assemblyPath = ResolvePath(project, configuration, targetFrameworkInformation.AssemblyPath);
                    var pdbPath = ResolvePath(project, configuration, targetFrameworkInformation.PdbPath);

                    metadataReferences.Add(new CompiledProjectMetadataReference(project, assemblyPath, pdbPath));
                }
                else
                {
                    // Find the default project exporter
                    var projectReferenceProvider = _projectReferenceProviders.GetOrAdd(project.LanguageServices.ProjectReferenceProvider, typeInfo =>
                    {
                        return LanguageServices.CreateService<IProjectReferenceProvider>(_serviceProvider, typeInfo);
                    });

                    Trace.TraceInformation("[{0}]: GetProjectReference({1}, {2}, {3})", project.LanguageServices.ProjectReferenceProvider.TypeName, name, targetFramework, configuration);

                    // Resolve the project export
                    IMetadataProjectReference projectReference = projectReferenceProvider.GetProjectReference(
                        project,
                        effectiveTargetFramework,
                        configuration,
                        projectExport.MetadataReferences,
                        projectExport.SourceReferences,
                        metadataReferences);

                    metadataReferences.Add(projectReference);

                    // Shared sources
                    foreach (var sharedFile in project.SharedFiles)
                    {
                        sourceReferences.Add(new SourceFileReference(sharedFile));
                    }
                }

                return new LibraryExport(metadataReferences, sourceReferences);
            });
        }

        private static string ResolvePath(Project project, string configuration, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (Path.DirectorySeparatorChar == '/')
            {
                path = path.Replace('\\', Path.DirectorySeparatorChar);
            }
            else
            {
                path = path.Replace('/', Path.DirectorySeparatorChar);
            }

            path = path.Replace("{configuration}", configuration);

            return Path.Combine(project.ProjectDirectory, path);
        }
    }
}
