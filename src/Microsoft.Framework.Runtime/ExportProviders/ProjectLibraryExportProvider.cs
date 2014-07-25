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

            // Get the composite library export provider
            var exportProvider = (ILibraryExportProvider)_serviceProvider.GetService(typeof(ILibraryExportProvider));

            // Get the exports for the project dependencies
            FrameworkName effectiveTargetFramework;
            var projectExport = ProjectExportProviderHelper.GetProjectDependenciesExport(
                exportProvider, 
                project, 
                targetFramework, 
                configuration, 
                out effectiveTargetFramework);

            // Find the default project exporter
            var projectExportProvider = _exportProviders.GetOrAdd(project.LanguageServices.ProjectExportProvider, typeInfo =>
            {
                return LanguageServices.CreateService<IProjectExportProvider>(_serviceProvider, typeInfo);
            });

            // Resolve the project export
            return projectExportProvider.GetProjectExport(project, effectiveTargetFramework, configuration, projectExport);
        }
    }
}
