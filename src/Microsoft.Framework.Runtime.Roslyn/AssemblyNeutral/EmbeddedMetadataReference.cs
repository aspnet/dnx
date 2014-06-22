// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class EmbeddedMetadataReference : RoslynMetadataReference, IMetadataEmbeddedReference
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

        public byte[] Contents { get; private set; }
    }
}
