// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationContext
    {
        /// <summary>
        /// The project associated with this compilation
        /// </summary>
        public Project Project { get; private set; }

        // Processed information
        public CSharpCompilation Compilation { get; set; }
        public IList<Diagnostic> Diagnostics { get; private set; }

        public IList<IMetadataReference> MetadataReferences { get; private set; }

        public IList<ICompileModule> Modules { get; private set; }

        public CompilationContext(CSharpCompilation compilation,
                                  IList<IMetadataReference> metadataReferences,
                                  IList<Diagnostic> diagnostics,
                                  Project project)
        {
            Compilation = compilation;
            MetadataReferences = metadataReferences;
            Diagnostics = diagnostics;
            Project = project;
            Modules = new List<ICompileModule>();
        }
    }
}
