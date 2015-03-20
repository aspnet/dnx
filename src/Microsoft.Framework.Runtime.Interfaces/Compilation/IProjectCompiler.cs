// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.Compilation
{
    public interface IProjectCompiler
    {
        IMetadataProjectReference CompileProject(
            Project project,
            ILibraryKey target, 
            Func<ILibraryExport> referenceResolver,
            Func<IList<ResourceDescriptor>> resourcesResolver);
    }
}
