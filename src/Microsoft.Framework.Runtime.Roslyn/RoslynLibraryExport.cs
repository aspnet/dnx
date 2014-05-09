// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynLibraryExport : LibraryExport
    {
        public RoslynLibraryExport(
            IList<IMetadataReference> metadataReferences,
            IList<ISourceReference> sourceReferences,
            CompilationContext compilationContext)
            : base(metadataReferences, sourceReferences)
        {
            CompilationContext = compilationContext;
        }

        public CompilationContext CompilationContext { get; set; }
    }
}
