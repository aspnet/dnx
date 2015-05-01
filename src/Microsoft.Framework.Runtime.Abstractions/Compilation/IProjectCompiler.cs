// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.Compilation
{
    public interface IProjectCompiler
    {
        IMetadataProjectReference CompileProject(
            ICompilationProject project,
            ILibraryKey target,
            Func<ILibraryExport> referenceResolver,
            Func<IList<ResourceDescriptor>> resourcesResolver);
    }
}
