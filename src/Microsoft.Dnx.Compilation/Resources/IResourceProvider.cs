// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation
{
    public interface IResourceProvider
    { 
        IList<ResourceDescriptor> GetResources(Project project);
    }
}
