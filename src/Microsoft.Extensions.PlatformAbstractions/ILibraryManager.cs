// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Extensions.PlatformAbstractions
{
    /// <summary>
    /// Provides access to the complete graph of dependencies for the application.
    /// </summary>
    public interface ILibraryManager
    {
        IEnumerable<Library> GetReferencingLibraries(string name);

        Library GetLibrary(string name);

        IEnumerable<Library> GetLibraries();
    }
}
