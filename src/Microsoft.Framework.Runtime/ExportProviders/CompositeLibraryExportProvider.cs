// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime
{
    public class CompositeLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IEnumerable<ILibraryExportProvider> _libraryExporters;

        public CompositeLibraryExportProvider(IEnumerable<ILibraryExportProvider> libraryExporters)
        {
            _libraryExporters = libraryExporters;
        }

        public ILibraryExport GetLibraryExport(ILibraryKey target)
        {
            return _libraryExporters.Select(r => r.GetLibraryExport(target))
                                             .FirstOrDefault(export => export != null);
        }
    }
}