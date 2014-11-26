// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Framework.Runtime
{
    public class ProjectLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<TypeInformation, IProjectReferenceProvider> _projectReferenceProviders = new Dictionary<TypeInformation, IProjectReferenceProvider>();
        private readonly Lazy<IAssemblyLoadContext> _projectLoadContext;

        public ProjectLibraryExportProvider(IProjectResolver projectResolver,
                                            IServiceProvider serviceProvider)
        {
            _projectResolver = projectResolver;
            _serviceProvider = serviceProvider;

            // REVIEW: In the future we should be able to throw this away
            _projectLoadContext = new Lazy<IAssemblyLoadContext>(() =>
             {
                 var factory = (IAssemblyLoadContextFactory)_serviceProvider.GetService(typeof(IAssemblyLoadContextFactory));
                 return factory.Create();
             });
        }

        public ILibraryExport GetLibraryExport(ILibraryKey target)
        {
            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(target.Name, out project))
            {
                return null;
            }

            Trace.TraceInformation("[{0}]: GetLibraryExport({1}, {2}, {3}, {4})", GetType().Name, target.Name, target.TargetFramework, target.Configuration, target.Aspect);

            var targetFrameworkInformation = project.GetTargetFramework(target.TargetFramework);

            // This is the target framework defined in the project. If there were no target frameworks
            // defined then this is the targetFramework specified
            if (targetFrameworkInformation.FrameworkName != null)
            {
                target = target.ChangeTargetFramework(targetFrameworkInformation.FrameworkName);
            }

            var key = Tuple.Create(
                target.Name,
                target.TargetFramework,
                target.Configuration,
                target.Aspect);

            var cache = (ICache)_serviceProvider.GetService(typeof(ICache));

            return cache.Get<ILibraryExport>(key, ctx =>
            {
                // Get the composite library export provider
                var exportProvider = (ILibraryExportProvider)_serviceProvider.GetService(typeof(ILibraryExportProvider));
                var libraryManager = (ILibraryManager)_serviceProvider.GetService(typeof(ILibraryManager));

                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                if (!string.IsNullOrEmpty(targetFrameworkInformation.AssemblyPath))
                {
                    var assemblyPath = ResolvePath(project, target.Configuration, targetFrameworkInformation.AssemblyPath);
                    var pdbPath = ResolvePath(project, target.Configuration, targetFrameworkInformation.PdbPath);

                    metadataReferences.Add(new CompiledProjectMetadataReference(project, assemblyPath, pdbPath));
                }
                else
                {
                    // Find the default project exporter
                    var projectReferenceProvider = _projectReferenceProviders.GetOrAdd(project.LanguageServices.ProjectReferenceProvider, typeInfo =>
                    {
                        return LanguageServices.CreateService<IProjectReferenceProvider>(_serviceProvider, _projectLoadContext.Value, typeInfo);
                    });

                    Trace.TraceInformation("[{0}]: GetProjectReference({1}, {2}, {3}, {4})", project.LanguageServices.ProjectReferenceProvider.TypeName, target.Name, target.TargetFramework, target.Configuration, target.Aspect);

                    // Get the exports for the project dependencies
                    var projectExport = new Lazy<ILibraryExport>(() => ProjectExportProviderHelper.GetExportsRecursive(
                        cache,
                        libraryManager,
                        exportProvider,
                        target,
                        dependenciesOnly: true));

                    // Resolve the project export
                    IMetadataProjectReference projectReference = projectReferenceProvider.GetProjectReference(
                        project,
                        target,
                        () => projectExport.Value,
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
