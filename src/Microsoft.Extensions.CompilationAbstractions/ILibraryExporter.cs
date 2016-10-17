// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Extensions.CompilationAbstractions
{
    /// <summary>
    /// Provides access to the complete graph of dependencies for the application.
    /// </summary>
    public interface ILibraryExporter
    {
        LibraryExport GetExport(string name);

        LibraryExport GetAllExports(string name);
    }
}
