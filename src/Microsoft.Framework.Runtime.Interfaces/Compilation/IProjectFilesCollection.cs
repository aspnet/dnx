using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    public interface IProjectFilesCollection
    {
        IEnumerable<string> BundleExcludeFiles { get; }
        IEnumerable<string> ContentFiles { get; }
        IEnumerable<string> PreprocessSourceFiles { get; }
        IEnumerable<string> ResourceFiles { get; }
        IEnumerable<string> SharedFiles { get; }
        IEnumerable<string> SourceFiles { get; }
    }
}