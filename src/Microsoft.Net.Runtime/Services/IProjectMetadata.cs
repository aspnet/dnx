using System.Collections.Generic;

namespace Microsoft.Net.Runtime.Services
{
    [AssemblyNeutral]
    public interface IProjectMetadata
    {
        IList<string> SourceFiles { get; }

        IList<string> References { get; }

        IList<string> Errors { get; }

        IList<byte[]> RawReferences { get; }
    }
}
