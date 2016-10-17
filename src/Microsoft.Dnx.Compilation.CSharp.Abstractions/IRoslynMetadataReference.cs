// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public interface IRoslynMetadataReference : IMetadataReference
    {
        MetadataReference MetadataReference { get; }
    }
}