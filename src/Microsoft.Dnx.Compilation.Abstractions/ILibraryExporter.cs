// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Compilation
{
    /// <summary>
    /// Provides access to the complete graph of dependencies for the application.
    /// </summary>
    public interface ILibraryExporter
    {
        LibraryExport GetExport(string name);

        LibraryExport GetExport(string name, string aspect);

        LibraryExport GetAllExports(string name);

        LibraryExport GetAllExports(string name, string aspect);
    }
}
