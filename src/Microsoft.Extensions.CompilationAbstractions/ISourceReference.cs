// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Extensions.CompilationAbstractions
{
    public interface ISourceReference
    {
        string Name { get; }
    }
}