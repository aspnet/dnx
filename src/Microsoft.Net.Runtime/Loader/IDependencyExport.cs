using System;
using System.Collections.Generic;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IDependencyExport
    {
        IList<IMetadataReference> MetadataReferences { get; set; }
        IList<ISourceReference> SourceReferences { get; set; }
    }
}
