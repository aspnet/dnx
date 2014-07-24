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
        private readonly Dictionary<TypeInformation, ILibraryExportProvider> _exportProviders = new Dictionary<TypeInformation, ILibraryExportProvider>();

        public ProjectLibraryExportProvider(IProjectResolver projectResolver, IServiceProvider serviceProvider)
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

            var exportProvider = _exportProviders.GetOrAdd(project.LanguageServices.LibraryExportProvider, typeInfo =>
            {
                return LanguageServices.CreateService<ILibraryExportProvider>(_serviceProvider, typeInfo);
            });

            return exportProvider.GetLibraryExport(name, targetFramework, configuration);
        }
    }
}
