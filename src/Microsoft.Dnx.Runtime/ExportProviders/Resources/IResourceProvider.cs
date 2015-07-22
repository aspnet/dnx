// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime
{
    public interface IResourceProvider
    { 
        IList<ResourceDescriptor> GetResources(Project project);
    }
}
