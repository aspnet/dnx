using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IProjectMetadata
    {
        IList<string> SourceFiles { get; }

        IList<string> References { get; }

        IList<string> Errors { get; }

        IList<string> Warnings { get; }

        IDictionary<string, byte[]> RawReferences { get; }

        IList<string> ProjectReferences { get; }
    }
}