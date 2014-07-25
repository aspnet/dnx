// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class ProjectLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<TypeInformation, IProjectExportProvider> _exportProviders = new Dictionary<TypeInformation, IProjectExportProvider>();
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

                var targetFrameworkInformation = project.GetTargetFramework(targetFramework);

                // This is the target framework defined in the project. If there were no target frameworks
                // defined then this is the targetFramework specified
                var effectiveTargetFramework = targetFrameworkInformation.FrameworkName ?? targetFramework;

                // Get the exports for the project dependencies
                ILibraryExport projectExport = ProjectExportProviderHelper.GetProjectDependenciesExport(
                    exportProvider,
                    project,
                    effectiveTargetFramework,
                    targetFrameworkInformation.Dependencies,
                    configuration);

                // Find the default project exporter
                var projectExportProvider = _exportProviders.GetOrAdd(project.LanguageServices.ProjectExportProvider, typeInfo =>
                {
                    return LanguageServices.CreateService<IProjectExportProvider>(_serviceProvider, typeInfo);
                });

                // Resolve the project export
                return projectExportProvider.GetProjectExport(
                    project, 
                    effectiveTargetFramework, 
                    configuration, 
                    projectExport);
            });
        }
    }
}
