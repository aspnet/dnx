// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Extensions.CompilationAbstractions
{
    public class EmbeddedMetadataReference : IMetadataEmbeddedReference
    {
        public EmbeddedMetadataReference(string name, byte[] buffer)
        {
            Name = name;
            Contents = buffer;
        }

        public string Name { get; }

        public byte[] Contents { get; }
    }
}