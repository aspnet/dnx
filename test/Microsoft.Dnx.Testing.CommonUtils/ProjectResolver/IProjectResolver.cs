// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Testing
{
    internal interface IProjectResolver
    {
        IEnumerable<string> SearchPaths { get; }

        bool TryResolveProject(string name, out Project project);
    }
}
