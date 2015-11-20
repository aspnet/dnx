// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.CompilationAbstractions
{
    public interface IProjectCompiler
    {
        IMetadataProjectReference CompileProject(
            CompilationProjectContext projectContext,
            Func<LibraryExport> referenceResolver,
            Func<IList<ResourceDescriptor>> resourcesResolver,
            string configuration);
    }
}
