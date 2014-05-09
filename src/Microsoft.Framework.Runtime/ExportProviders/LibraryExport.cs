// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    public class LibraryExport : ILibraryExport
    {
        public LibraryExport(string name, string path)
            : this(new MetadataFileReference(name, path))
        {
        }

        public LibraryExport(IMetadataReference metadataReference)
        {
            MetadataReferences = new List<IMetadataReference>
            {
                metadataReference
            };

            SourceReferences = new List<ISourceReference>();
        }

        public LibraryExport(
            IList<IMetadataReference> metadataReferences,
            IList<ISourceReference> sourceReferences)
        {
            MetadataReferences = metadataReferences;
            SourceReferences = sourceReferences;
        }

        public IList<IMetadataReference> MetadataReferences { get; set; }

        public IList<ISourceReference> SourceReferences { get; set; }
    }
}
