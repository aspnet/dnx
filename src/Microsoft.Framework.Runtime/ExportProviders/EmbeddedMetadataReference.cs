// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Framework.Runtime
{
    internal class EmbeddedMetadataReference : IMetadataEmbeddedReference
    {
        public EmbeddedMetadataReference(string name, byte[] buffer)
        {
            Name = name;
            Contents = buffer;
        }

        public string Name { get; private set; }

        public byte[] Contents { get; private set; }
    }
}
