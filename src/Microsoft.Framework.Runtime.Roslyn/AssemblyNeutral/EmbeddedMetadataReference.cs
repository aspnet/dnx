// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class EmbeddedMetadataReference : RoslynMetadataReference, IMetadataEmbeddedReference
    {
        public EmbeddedMetadataReference(TypeCompilationContext context)
            : base(context.AssemblyName, context.RealOrShallowReference())
        {
            Contents = new byte[context.OutputBytes.Length];
            Array.Copy(context.OutputBytes, Contents, context.OutputBytes.Length);
        }

        public byte[] Contents { get; private set; }
    }
}
