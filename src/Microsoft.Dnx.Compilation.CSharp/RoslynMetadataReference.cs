// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class RoslynMetadataReference : IRoslynMetadataReference
    {
        public RoslynMetadataReference(string name, MetadataReference metadataReference)
        {
            Name = name;
            MetadataReference = metadataReference;
        }

        public string Name
        {
            get;
            private set;
        }

        public MetadataReference MetadataReference { get; private set; }

        public override string ToString()
        {
            return MetadataReference.ToString();
        }
    }
}
