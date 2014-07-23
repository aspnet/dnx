// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectMetadataProvider : IProjectMetadataProvider
    {
        private readonly ILibraryExportProvider _libraryExportProvider;

        public RoslynProjectMetadataProvider(ILibraryExportProvider libraryExportProvider)
        {
            _libraryExportProvider = libraryExportProvider;
        }

        public IProjectMetadata GetProjectMetadata(string name, FrameworkName targetFramework, string configuration)
        {
            var context = GetCompilationContext(name, targetFramework, configuration);

            if (context == null)
            {
                return null;
            }

            return new RoslynProjectMetadata(context);
        }

        private CompilationContext GetCompilationContext(string name, FrameworkName targetFramework, string configuration)
        {
            var export = _libraryExportProvider.GetLibraryExport(name, targetFramework, configuration);

            if (export == null)
            {
                return null;
            }

            foreach (var projectReference in export.MetadataReferences.OfType<RoslynProjectReference>())
            {
                if (string.Equals(projectReference.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectReference.CompilationContext;
                }
            }

            return null;
        }
    }
}
