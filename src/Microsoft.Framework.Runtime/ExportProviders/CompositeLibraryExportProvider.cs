// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class CompositeLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IEnumerable<ILibraryExportProvider> _libraryExporters;

        public CompositeLibraryExportProvider(IEnumerable<ILibraryExportProvider> libraryExporters)
        {
            _libraryExporters = libraryExporters;
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
        {
            return _libraryExporters.Select(r => r.GetLibraryExport(name, targetFramework, configuration))
                                             .FirstOrDefault(export => export != null);
        }
    }
}
