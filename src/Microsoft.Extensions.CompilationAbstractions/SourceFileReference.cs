// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Extensions.CompilationAbstractions
{
    public class SourceFileReference : ISourceFileReference
    {
        public SourceFileReference(string path)
        {
            // Unique name of the reference
            Name = path;
            Path = path;
        }

        public string Name { get; }

        public string Path { get; }
    }
}