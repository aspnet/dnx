using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface ILibraryExport
    {
        IList<IMetadataReference> MetadataReferences { get; }
        IList<ISourceReference> SourceReferences { get; }
    }
}
