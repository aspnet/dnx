// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Dnx.Compilation
{
    public class LibraryExport
    {
        public static readonly LibraryExport Empty = new LibraryExport(metadataReference: null);

        public LibraryExport(IMetadataReference metadataReference)
            : this(new List<IMetadataReference>() { metadataReference }, sourceReferences: null, analyzerReferences: null)
        {
        }

        public LibraryExport(IList<IMetadataReference> metadataReferences, IList<ISourceReference> sourceReferences)
            : this(metadataReferences, sourceReferences, analyzerReferences: null)
        {
        }

        public LibraryExport(IList<IMetadataReference> metadataReferences, IList<ISourceReference> sourceReferences, IList<IAnalyzerReference> analyzerReferences)
        {
            MetadataReferences = metadataReferences ?? new List<IMetadataReference>();
            SourceReferences = sourceReferences ?? new List<ISourceReference>();
            AnalyzerReferences = analyzerReferences ?? new List<IAnalyzerReference>();
        }

        public IList<IMetadataReference> MetadataReferences { get; }

        public IList<ISourceReference> SourceReferences { get; }

        public IList<IAnalyzerReference> AnalyzerReferences { get; }
    }
}