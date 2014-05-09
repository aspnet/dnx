// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface ILibraryManager
    {
        ILibraryExport GetLibraryExport(string name);

        IEnumerable<ILibraryInformation> GetReferencingLibraries(string name);

        ILibraryInformation GetLibraryInformation(string name);

        IEnumerable<ILibraryInformation> GetLibraries();
    }
}
