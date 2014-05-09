// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class EmbeddedMetadataReference : RoslynMetadataReference
    {
        public EmbeddedMetadataReference(TypeCompilationContext context)
            : base(context.AssemblyName, context.RealOrShallowReference())
        {
            using (var ms = new MemoryStream((int)context.OutputStream.Length))
            {
                // This stream is always seekable
                context.OutputStream.Position = 0;
                context.OutputStream.CopyTo(ms);
                Contents = ms.ToArray();
            }
        }

        public EmbeddedMetadataReference(string name, byte[] buffer)
            : base(name, new MetadataImageReference(buffer))
        {
            Contents = buffer;
        }

        public byte[] Contents { get; private set; }
    }
}
