// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class BeforeCompileContext
    {
        public CSharpCompilation Compilation { get; set; }

        public ProjectContext ProjectContext { get; set; }

        public IList<ResourceDescription> Resources { get; set; }

        public IList<Diagnostic> Diagnostics { get; set; }

        public IList<IMetadataReference> MetadataReferences { get; set; }
    }
}
