// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public interface IBeforeCompileContext
    {
        CSharpCompilation Compilation { get; set; }

        IProjectContext ProjectContext { get; }

        IList<ResourceDescription> Resources { get; }

        IList<Diagnostic> Diagnostics { get; }

        IEnumerable<IMetadataReference> References { get; }
    }
}
