// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime
{
    public interface ILibraryManager
    {
        ILibraryExport GetLibraryExport(string name);

        ILibraryExport GetAllExports(string name);

        IEnumerable<ILibraryInformation> GetReferencingLibraries(string name);

        ILibraryInformation GetLibraryInformation(string name);

        IEnumerable<ILibraryInformation> GetLibraries();

        ILibraryExport GetLibraryExport(string name, string aspect);

        ILibraryExport GetAllExports(string name, string aspect);

        IEnumerable<ILibraryInformation> GetReferencingLibraries(string name, string aspect);

        ILibraryInformation GetLibraryInformation(string name, string aspect);

        IEnumerable<ILibraryInformation> GetLibraries(string aspect);
    }
}
